#!/usr/bin/env ruby

require "csv"
require "fileutils"

ROOT = File.expand_path("..", __dir__)
OUTPUT_DIR = File.join(ROOT, "docs", "migration", "data-oriented-ecs")

TypeDeclaration = Struct.new(:source, :line, :kind, :name, :bases, :body, keyword_init: true) do
  def key
    "#{source}##{name}"
  end
end

def source_files(*roots)
  roots.flat_map { |root| Dir.glob(File.join(ROOT, root, "**", "*.cs")) }
    .uniq
    .sort
end

def relative(path)
  path.delete_prefix("#{ROOT}/")
end

def read_source(path)
  File.read(path, encoding: "UTF-8").scrub
end

def strip_comments(text)
  text.gsub(%r{/\*.*?\*/}m, "").gsub(%r{//.*$}, "")
end

def extract_body(text, declaration_end)
  opening = text.index("{", declaration_end)
  return "" unless opening

  depth = 0
  in_string = false
  escaped = false
  index = opening
  while index < text.length
    character = text[index]
    if in_string
      if escaped
        escaped = false
      elsif character == "\\"
        escaped = true
      elsif character == '"'
        in_string = false
      end
    elsif character == '"'
      in_string = true
    elsif character == "{"
      depth += 1
    elsif character == "}"
      depth -= 1
      return text[(opening + 1)...index] if depth.zero?
    end
    index += 1
  end

  ""
end

def declarations(paths)
  declarations = []
  pattern = /\b(class|interface|struct|record(?:\s+struct|\s+class)?)\s+([A-Za-z_]\w*)(?:\s*<[^>{}]+>)?\s*(?::\s*([^\{\r\n]+))?\s*\{/m

  paths.each do |path|
    raw = read_source(path)
    text = strip_comments(raw)
    text.to_enum(:scan, pattern).each do
      match = Regexp.last_match
      declarations << TypeDeclaration.new(
        source: relative(path),
        line: text[0...match.begin(0)].count("\n") + 1,
        kind: match[1].gsub(/\s+/, " "),
        name: match[2],
        bases: (match[3] || "").split(",").map { |base| base.strip.split("<").first.split(".").last },
        body: extract_body(text, match.end(0) - 1)
      )
    end
  end

  declarations
end

def component_target(declaration)
  body = declaration.body
  without_owner = body.gsub(/public\s+(?:[\w.<>?,\[\]]+\s+)?Owner\s*\{[^}]*\}/m, "")
  meaningful = without_owner.gsub(/\[[^\]]+\]/m, "").gsub(/[{};\s]/, "")

  return "Empty tag" if meaningful.empty?
  return "Dynamic buffer handle plus unmanaged component" if body.match?(/\b(List|Dictionary|HashSet|Queue|Stack|IList|IEnumerable)\s*</)
  return "Catalog definition referenced by compact ID" if body.match?(/\b(CardBase|EnemyBase|EnemyAttackBase|EquipmentBase|MedalBase|AchievementBase|TemperanceBase)\b/)
  return "Resource ID plus unmanaged presentation component" if body.match?(/\b(Texture2D|RenderTarget2D|SpriteFont|SoundEffect|Song|Effect|GraphicsDevice|SpriteBatch)\b/)
  return "Hot unmanaged component split from catalog/buffer state" if body.match?(/\b(string|Action|Func|Delegate|object|Entity)\b|\[\]/)

  "Unmanaged component struct"
end

def component_owner(declaration)
  value = "#{declaration.source} #{declaration.name}"
  return "ECS-020" if value.match?(/\b(Transform|ParentTransform|ParallaxLayer|PositionTween|UIElement|Animation|Sprite|SceneState|OwnedByScene|DontDestroyOnLoad|DontDestroyOnReload|HP|Courage|ActionPoints|Temperance|Threat|Intellect|MaxHandSize|InputState|InputContext|CursorState|ActorPresentationState|BattlePresentationTransform)\b/i)
  return "ECS-044" if value.match?(/Climb|Shop|Reward|Achievement|Tutorial|Dialog|Quest|WayStation|Booster|Save|RunDeck/i)
  return "ECS-043" if value.match?(/Equipment|Medal|Passive|Modifier|Replacement|Temperance|Vigor|Bleed|Frostbite|Poison/i)
  return "ECS-042" if value.match?(/Combat|Enemy|Attack|Block|Battle|Phase|Threat|HP\b|Courage|ActionPoints|Ambush/i)
  return "ECS-041" if value.match?(/Card|Deck|Hand|Discard|DrawPile|Exhaust|Pledge|Recoil|Seal|Curse|Frozen|Shackle|Plunder|Payment/i)
  return "ECS-040" if value.match?(/Scene|Input|Cursor|HotKey|UIElement|ModalInput|OwnedByScene|DontDestroy/i)

  "ECS-045"
end

def event_owner(declaration)
  value = "#{declaration.source} #{declaration.name}"
  return "ECS-044" if value.match?(/Climb|Shop|Achievement|Tutorial|Dialog|Quest|Reward|Booster|Loadout|Gold|Currency/i)
  return "ECS-040" if value.match?(/Scene|PlayerInput|PlayerCommand|InputEnabled/i)
  return "ECS-045" if value.match?(/Visual|Render|Animation|Rumble|Shockwave|Audio|Music|Sfx|LocationName|Transition/i)
  return "ECS-043" if value.match?(/Equipment|Medal|Passive|Replacement|Temperance|Frostbite|Poison|Tribulation/i)
  return "ECS-042" if value.match?(/Combat|Attack|Enemy|Block|Hp|Threat|Damage|Phase|Ambush|PlayerDied/i)

  "ECS-041"
end

def system_owner(declaration)
  name = declaration.name
  source = declaration.source
  value = "#{source} #{name}"
  return "ECS-045" if name.match?(/Display|Render|Visual|Animation|Tween|Parallax|Tooltip|Profiler|Debug|Audio|Rumble|Shockwave|Overlay|Layout|Portrait|CursorTrail/i)
  return "ECS-044" if value.match?(/Climb|WayStation|Achievement|Tutorial|Dialog|Quest|Reward|Booster|Shop|TitleMenu|RunDeck|Collection|Save/i)
  return "ECS-043" if name.match?(/Equipment|Medal|Passive|Replacement|Modifier|Temperance|Vigor|Bleed|Poison|Frostbite|Scorched|Brittle|Intimidat|Status/i)
  return "ECS-041" if name.match?(/Card|Deck|Hand|Discard|Draw|Exhaust|Pledge|Recoil|Seal|Curse|Shackle|Plunder|Payment|ActionPoint/i)
  return "ECS-042" if name.match?(/Combat|Enemy|Attack|Block|Battle|Hp|Health|Threat|Damage|Defeat|Ambush|Phase/i)
  return "ECS-040" if name.match?(/Global|SceneSystem|Lifecycle|Input|Cursor|UIElement|HotKey|Modal|DeleteEntity/i)
  return "ECS-042" if source.include?("/BattleScene/")
  return "ECS-044" if source.match?(%r{/Scenes/(ClimbScene|WayStationScene|AchievementScene|TitleMenuScene)/})

  "ECS-040"
end

def object_owner(declaration)
  case declaration.source
  when %r{ECS/Objects/Cards/}
    "ECS-030"
  when %r{ECS/Objects/Enemies/}
    "ECS-031"
  when %r{ECS/Objects/(Equipment|Medals)/}
    "ECS-032"
  when %r{ECS/Objects/Temperance/}
    "ECS-043"
  else
    "ECS-044"
  end
end

def object_target(declaration)
  return "Generated definition module and static handler" if declaration.bases.any? { |base| base.end_with?("Base") }
  return "Generated provider table" if declaration.kind == "interface" || declaration.bases.any? { |base| base.end_with?("Provider") }

  "Catalog/helper folded into owning domain"
end

def transitive_systems(all_declarations)
  system_names = ["System"]
  changed = true
  while changed
    changed = false
    all_declarations.each do |declaration|
      next if system_names.include?(declaration.name)
      next unless declaration.bases.any? { |base| system_names.include?(base) }

      system_names << declaration.name
      changed = true
    end
  end

  all_declarations.select do |declaration|
    declaration.name != "System" && declaration.bases.any? { |base| system_names.include?(base) }
  end
end

def event_subscriptions(paths)
  rows = []
  paths.each do |path|
    text = strip_comments(read_source(path))
    counters = Hash.new(0)
    text.to_enum(:scan, /EventManager\.Subscribe\s*(?:<\s*([^>]+)\s*>)?\s*\(/).each do
      match = Regexp.last_match
      event_type = match[1]&.strip || "<inferred-delegate-type>"
      counters[event_type] += 1
      source = relative(path)
      owner_stub = TypeDeclaration.new(source: source, name: File.basename(path, ".cs"))
      rows << {
        "Key" => "#{source}##{event_type}##{counters[event_type]}",
        "Source" => source,
        "Line" => text[0...match.begin(0)].count("\n") + 1,
        "EventType" => event_type,
        "Occurrence" => counters[event_type],
        "OwnerTask" => system_owner(owner_stub),
        "Target" => "World-owned typed event stream or generated route"
      }
    end
  end
  rows
end

def stable_domain_ids
  path = File.join(ROOT, "ECS", "Data", "Ids", "GameIds.cs")
  text = strip_comments(read_source(path))
  domains = %w[CardId EnemyId EnemyAttackId EquipmentId MedalId]

  domains.flat_map do |domain|
    enum_match = text.match(/public enum #{domain}\s*:\s*ushort\s*\{(.*?)\n\}/m)
    raise "#{domain} must be an explicit ushort enum" unless enum_match

    values = enum_match[1].scan(/^\s*([A-Za-z_]\w*)\s*=\s*(\d+)\s*,\s*$/).to_h
    keys = text.scan(/#{domain}\.([A-Za-z_]\w*)\s*=>\s*"([^"]+)"/).to_h
    raise "#{domain} enum/key coverage differs" unless values.keys.sort == keys.keys.sort
    raise "#{domain} contains duplicate numeric values" unless values.values.uniq.length == values.length
    raise "#{domain} contains duplicate string keys" unless keys.values.uniq.length == keys.length

    values.map do |name, value|
      {
        "Key" => "#{domain}##{name}",
        "Domain" => domain,
        "Name" => name,
        "NumericValue" => value,
        "StringKey" => keys.fetch(name)
      }
    end
  end.sort_by { |row| [row["Domain"], row["NumericValue"].to_i] }
end

def write_csv(name, headers, rows)
  path = File.join(OUTPUT_DIR, name)
  CSV.open(path, "wb", write_headers: true, headers: headers) do |csv|
    rows.each { |row| csv << headers.map { |header| row.fetch(header) } }
  end
end

def current_rows
  ecs_files = source_files("ECS").reject { |path| relative(path).start_with?("ECS/DataOriented/") }
  all = declarations(ecs_files)

  components = all.select { |declaration| declaration.bases.include?("IComponent") }.map do |declaration|
    {
      "Key" => declaration.key,
      "Source" => declaration.source,
      "Line" => declaration.line,
      "LegacyType" => declaration.name,
      "OwnerTask" => component_owner(declaration),
      "TargetClassification" => component_target(declaration)
    }
  end.sort_by { |row| row["Key"] }

  events = all.select { |declaration| declaration.source.start_with?("ECS/Events/") }.map do |declaration|
    {
      "Key" => declaration.key,
      "Source" => declaration.source,
      "Line" => declaration.line,
      "LegacyType" => declaration.name,
      "OwnerTask" => event_owner(declaration),
      "TargetClassification" => declaration.kind == "interface" ? "Generated provider/routing contract" : "Unmanaged event, command, or catalog data"
    }
  end.sort_by { |row| row["Key"] }

  systems = transitive_systems(all).map do |declaration|
    {
      "Key" => declaration.key,
      "Source" => declaration.source,
      "Line" => declaration.line,
      "LegacyType" => declaration.name,
      "OwnerTask" => system_owner(declaration),
      "TargetClassification" => "IGameSystem with generated descriptor"
    }
  end.sort_by { |row| row["Key"] }

  objects = all.select { |declaration| declaration.source.start_with?("ECS/Objects/") }.map do |declaration|
    {
      "Key" => declaration.key,
      "Source" => declaration.source,
      "Line" => declaration.line,
      "LegacyType" => declaration.name,
      "OwnerTask" => object_owner(declaration),
      "TargetClassification" => object_target(declaration)
    }
  end.sort_by { |row| row["Key"] }

  subscriptions = event_subscriptions(ecs_files + [File.join(ROOT, "Game1.cs")]).sort_by { |row| row["Key"] }

  {
    "components.csv" => components,
    "events.csv" => events,
    "systems.csv" => systems,
    "object-behaviors.csv" => objects,
    "event-subscriptions.csv" => subscriptions,
    "stable-domain-ids.csv" => stable_domain_ids
  }
end

def write_ledgers(rows_by_file)
  FileUtils.mkdir_p(OUTPUT_DIR)
  rows_by_file.each do |name, rows|
    write_csv(name, rows.first&.keys || [], rows)
  end

  counts = rows_by_file.transform_values(&:length)
  findings = <<~MARKDOWN
    # Data-Oriented ECS Migration Ledger Findings

    Generated by `scripts/audit-data-oriented-ecs-ledgers.rb --write`.
    Verify with `scripts/audit-data-oriented-ecs-ledgers.rb`.

    | Ledger | Classified entries |
    | --- | ---: |
    | Components | #{counts.fetch("components.csv")} |
    | Event types and event support contracts | #{counts.fetch("events.csv")} |
    | Event subscriptions | #{counts.fetch("event-subscriptions.csv")} |
    | Systems | #{counts.fetch("systems.csv")} |
    | Object/content behavior types | #{counts.fetch("object-behaviors.csv")} |
    | Stable domain IDs | #{counts.fetch("stable-domain-ids.csv")} |

    Every discovered entry has exactly one owning task and one target classification. The
    audit compares stable `source#type` keys (and source/event occurrence keys for
    subscriptions), rejects missing or extra rows, and rejects blank assignments. Stable
    domain ID rows are compared in full, freezing their numeric values and string keys.
  MARKDOWN
  File.write(File.join(OUTPUT_DIR, "findings.md"), findings)
end

def audit(rows_by_file)
  failures = []
  rows_by_file.each do |name, expected_rows|
    path = File.join(OUTPUT_DIR, name)
    unless File.exist?(path)
      failures << "missing ledger #{relative(path)}"
      next
    end

    actual_rows = CSV.read(path, headers: true).map(&:to_h)
    expected_keys = expected_rows.map { |row| row.fetch("Key") }
    actual_keys = actual_rows.map { |row| row.fetch("Key") }
    duplicate_keys = actual_keys.group_by(&:itself).select { |_key, values| values.length > 1 }.keys
    missing = expected_keys - actual_keys
    extra = actual_keys - expected_keys
    blank = if name == "stable-domain-ids.csv"
      []
    else
      actual_rows.select do |row|
        row.fetch("OwnerTask", "").strip.empty? || row.fetch("TargetClassification", row.fetch("Target", "")).strip.empty?
      end
    end

    failures << "#{name}: duplicate keys #{duplicate_keys.join(', ')}" unless duplicate_keys.empty?
    failures << "#{name}: missing #{missing.join(', ')}" unless missing.empty?
    failures << "#{name}: extra #{extra.join(', ')}" unless extra.empty?
    failures << "#{name}: blank assignments #{blank.map { |row| row['Key'] }.join(', ')}" unless blank.empty?
    if name == "stable-domain-ids.csv" && actual_rows != expected_rows.map { |row| row.transform_values(&:to_s) }
      failures << "#{name}: numeric values, declaration order, or string keys changed"
    end
  end

  if failures.empty?
    total = rows_by_file.values.sum(&:length)
    puts "Migration ledger audit passed: #{total} entries classified exactly once."
    exit 0
  end

  warn failures.join("\n")
  warn "Run scripts/audit-data-oriented-ecs-ledgers.rb --write after intentionally updating assignments."
  exit 1
end

rows = current_rows
if ARGV == ["--write"]
  write_ledgers(rows)
  puts "Wrote migration ledgers to #{relative(OUTPUT_DIR)}."
elsif ARGV.empty?
  audit(rows)
else
  warn "Usage: scripts/audit-data-oriented-ecs-ledgers.rb [--write]"
  exit 2
end
