import os
import traceback

from gi.repository import Gegl, Gio, Gimp


SUPPORTED_EXTENSIONS = {
    ".bmp",
    ".jpeg",
    ".jpg",
    ".png",
    ".tif",
    ".tiff",
    ".webp",
}


def env_float(name, default):
    value = os.environ.get(name)
    if value is None or value == "":
        return default
    return float(value)


def env_int(name, default):
    value = os.environ.get(name)
    if value is None or value == "":
        return default
    return int(value)


def env_bool(name, default):
    value = os.environ.get(name)
    if value is None or value == "":
        return default
    return value.lower() not in {"0", "false", "no", "off"}


def run_pdb(name, **properties):
    procedure = Gimp.get_pdb().lookup_procedure(name)
    config = procedure.create_config()

    for key, value in properties.items():
        config.set_property(key, value)

    result = procedure.run(config)
    status = result.index(0)
    if status != Gimp.PDBStatusType.SUCCESS:
        raise RuntimeError(f"{name} failed with status {status}")

    return result


def rgba_tuple(color):
    rgba = color.get_rgba()
    return rgba.red, rgba.green, rgba.blue, rgba.alpha


def is_near_white(drawable, x, y, threshold):
    red, green, blue, _ = rgba_tuple(drawable.get_pixel(int(x), int(y)))
    floor = 1.0 - (threshold / 255.0)
    return red >= floor and green >= floor and blue >= floor


def edge_points(width, height, samples):
    if samples < 2:
        return [(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)]

    points = []
    for index in range(samples):
        t = index / (samples - 1)
        x = round(t * (width - 1))
        y = round(t * (height - 1))
        points.extend([(x, 0), (x, height - 1), (0, y), (width - 1, y)])

    unique_points = []
    for point in points:
        if point not in unique_points:
            unique_points.append(point)

    return unique_points


def output_path_for(input_path, input_root, output_root):
    if os.path.isfile(input_root) and output_root.lower().endswith(".png"):
        return output_root

    if os.path.isfile(input_root):
        relative = os.path.basename(input_path)
    else:
        relative = os.path.relpath(input_path, input_root)

    relative_base, _ = os.path.splitext(relative)
    return os.path.join(output_root, relative_base + ".png")


def discover_inputs(input_root, recursive):
    if os.path.isfile(input_root):
        extension = os.path.splitext(input_root)[1].lower()
        if extension in SUPPORTED_EXTENSIONS:
            return [input_root]
        raise RuntimeError(f"Unsupported input file extension: {input_root}")

    if not os.path.isdir(input_root):
        raise RuntimeError(f"Input path does not exist: {input_root}")

    inputs = []
    if recursive:
        walker = os.walk(input_root)
        for root, _, files in walker:
            for filename in files:
                path = os.path.join(root, filename)
                extension = os.path.splitext(path)[1].lower()
                if extension in SUPPORTED_EXTENSIONS:
                    inputs.append(path)
    else:
        for filename in os.listdir(input_root):
            path = os.path.join(input_root, filename)
            extension = os.path.splitext(path)[1].lower()
            if os.path.isfile(path) and extension in SUPPORTED_EXTENSIONS:
                inputs.append(path)

    return sorted(inputs)


def load_image(path):
    return run_pdb(
        "gimp-file-load",
        **{
            "run-mode": Gimp.RunMode.NONINTERACTIVE,
            "file": Gio.File.new_for_path(path),
        },
    ).index(1)


def export_png(image, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    run_pdb(
        "file-png-export",
        **{
            "run-mode": Gimp.RunMode.NONINTERACTIVE,
            "image": image,
            "file": Gio.File.new_for_path(path),
            "options": None,
            "interlaced": False,
            "compression": 9,
            "bkgd": False,
            "offs": False,
            "phys": False,
            "time": False,
            "save-transparent": True,
            "optimize-palette": False,
            "format": "auto",
            "include-exif": False,
            "include-iptc": False,
            "include-xmp": False,
            "include-color-profile": True,
            "include-thumbnail": False,
            "include-comment": False,
        },
    )


def get_work_layer(image):
    layers = image.get_layers()
    if not layers:
        raise RuntimeError("Image has no layers")

    if len(layers) == 1:
        return layers[0]

    return image.merge_visible_layers(Gimp.MergeType.EXPAND_AS_NECESSARY)


def remove_edge_connected_white(image, layer, threshold, feather, edge_samples):
    Gimp.context_set_sample_threshold(threshold / 255.0)
    Gimp.context_set_sample_merged(False)
    Gimp.context_set_sample_transparent(False)
    Gimp.Selection.none(image)

    operation = Gimp.ChannelOps.REPLACE
    seed_count = 0
    for x, y in edge_points(image.get_width(), image.get_height(), edge_samples):
        if is_near_white(layer, x, y, threshold):
            image.select_contiguous_color(operation, layer, float(x), float(y))
            operation = Gimp.ChannelOps.ADD
            seed_count += 1

    if seed_count == 0:
        return 0

    if feather > 0:
        Gimp.Selection.feather(image, feather)

    layer.edit_clear()
    Gimp.Selection.none(image)
    image.autocrop(layer)
    return seed_count


def process_file(input_path, output_path, threshold, feather, edge_samples):
    image = None
    Gimp.context_push()
    try:
        image = load_image(input_path)
        image.undo_disable()

        layer = get_work_layer(image)
        layer.add_alpha()

        seed_count = remove_edge_connected_white(
            image,
            layer,
            threshold,
            feather,
            edge_samples,
        )
        export_png(image, output_path)

        print(
            "wrote {output} ({width}x{height}, seeds={seeds})".format(
                output=output_path,
                width=image.get_width(),
                height=image.get_height(),
                seeds=seed_count,
            )
        )
    finally:
        Gimp.context_pop()
        if image is not None:
            image.delete()


def main():
    input_root = os.environ.get("GIMP_BG_INPUT")
    output_root = os.environ.get("GIMP_BG_OUTPUT")
    if not input_root or not output_root:
        raise RuntimeError("GIMP_BG_INPUT and GIMP_BG_OUTPUT must be set")

    input_root = os.path.abspath(input_root)
    output_root = os.path.abspath(output_root)
    threshold = max(0.0, min(255.0, env_float("GIMP_BG_THRESHOLD", 35.0)))
    feather = max(0.0, env_float("GIMP_BG_FEATHER", 0.5))
    edge_samples = max(2, env_int("GIMP_BG_EDGE_SAMPLES", 9))
    recursive = env_bool("GIMP_BG_RECURSIVE", True)
    fail_fast = env_bool("GIMP_BG_FAIL_FAST", False)

    input_paths = discover_inputs(input_root, recursive)
    if not input_paths:
        raise RuntimeError(f"No supported images found in {input_root}")

    failures = []
    for input_path in input_paths:
        output_path = output_path_for(input_path, input_root, output_root)
        try:
            process_file(input_path, output_path, threshold, feather, edge_samples)
        except Exception as error:
            failures.append((input_path, error))
            print(f"failed {input_path}: {error}")
            traceback.print_exc()
            if fail_fast:
                raise

    if failures:
        raise RuntimeError(f"{len(failures)} image(s) failed")


main()
