#!/usr/bin/env ruby

require "csv"
require "optparse"

ROOT = File.expand_path("..", __dir__)
MIGRATION = File.join(ROOT, "docs", "migration", "data-oriented-ecs")

DOMAIN_DOCUMENTS = {
  "ECS-040" => "ecs040-global-ui-mapping.md",
  "ECS-041" => "ecs-041-card-gameplay-mapping.md",
  "ECS-042" => "ecs042-combat-mapping.md",
  "ECS-043" => "ecs043-effects-equipment-medals.md",
  "ECS-044" => "ecs044-meta-game-mapping.md",
  "ECS-045" => "ecs045-presentation-rendering-mapping.md"
}.freeze

# This is deliberately an explicit scheduler allowlist. Compatibility names, event-only
# consumers, consolidated legacy names, and external draw/audio adapters do not belong here.
OPERATIONAL_SYSTEMS = {
  "ECS-040" => %w[
    PlayerInputSystem ModalInputSuppressionSystem UIInteractionSystem HotKeySystem
    SceneLifecycleSystem SceneLoadingCoordinatorSystem TimerSchedulerSystem
    HighlightSettingsSystem EventQueueSystem
  ],
  "ECS-041" => %w[DeckManagementSystem],
  "ECS-042" => %w[AttackResolutionSystem EnemyAttackProgressManagementSystem],
  # ECS-043 currently has route-owned consumers only; it schedules no per-frame system.
  "ECS-043" => [],
  "ECS-044" => %w[
    ClimbRuntimeSystem WayStationRuntimeSystem RewardRuntimeSystem AchievementRuntimeSystem
    TutorialRuntimeSystem DialogueRuntimeSystem RunLifecycleRuntimeSystem
  ],
  "ECS-045" => %w[
    PositionTweenPresentationSystem ParallaxPresentationSystem JigglePulsePresentationSystem
    VisualEffectPresentationSystem SpriteRenderExtractionSystem
  ]
}.freeze

LEGACY_PATTERNS = {
  "entity references" => /\bEntity\b/,
  "component lookups" => /\b(?:GetComponent|TryGetComponent|HasComponent|GetEntitiesWithComponent|GetEntitiesWithComponents)\s*</,
  "LINQ queries" => /using\s+System\.Linq\s*;|\.(?:Where|Select|SelectMany|OrderBy|OrderByDescending|ThenBy|ThenByDescending|Intersect|ToList|ToArray|First|FirstOrDefault|Single|SingleOrDefault|Any|All|Count)\s*\(/,
}.freeze

NEW_SOURCE_PATTERNS = {
  "old ECS namespace dependency" => /Crusaders30XX\.ECS\.Core/,
  "static EventManager dependency" => /\bEventManager\b/,
  "static typed event stream" => /\bstatic\s+(?:readonly\s+)?(?:EventStream|IEventStream)\s*</,
  "direct hardware state" => /\b(?:MouseState|GamePadState|KeyboardState)\b|\b(?:Mouse|GamePad|Keyboard)\.GetState\s*\(/,
  "LINQ query" => /using\s+System\.Linq\s*;|\.(?:Where|Select|SelectMany|OrderBy|OrderByDescending|ThenBy|ThenByDescending|Intersect|ToList|First|FirstOrDefault)\s*\(/,
  "legacy component lookup" => /\b(?:GetComponent|TryGetComponent|HasComponent|GetEntitiesWithComponent|GetEntitiesWithComponents)\s*</,
}.freeze

LineFinding = Struct.new(:source, :line, :text, keyword_init: true)

options = { write: nil, strict: true }
OptionParser.new do |parser|
  parser.banner = "Usage: scripts/audit-data-oriented-ecs-completeness.rb [options]"
  parser.on("--write [PATH]", "Write the full Markdown inventory") do |path|
    options[:write] = path || File.join(MIGRATION, "ecs046-pre-audit-findings.md")
  end
  parser.on("--no-strict", "Report blockers without returning a failing exit code") do
    options[:strict] = false
  end
end.parse!

def relative(path)
  path.delete_prefix("#{ROOT}/")
end

def cs_files(root, exclude_data_oriented: false)
  Dir.glob(File.join(ROOT, root, "**", "*.cs")).sort.reject do |path|
    exclude_data_oriented && relative(path).start_with?("ECS/DataOriented/")
  end
end

def scrubbed_lines(path)
  File.read(path, encoding: "UTF-8").scrub.lines
end

def line_findings(paths, pattern)
  paths.flat_map do |path|
    scrubbed_lines(path).each_with_index.each_with_object([]) do |(line, index), findings|
      next unless line.match?(pattern)
      next if line.lstrip.start_with?("//")
      findings << LineFinding.new(source: relative(path), line: index + 1, text: line.strip)
    end
  end
end

def ledger_rows(name, owners)
  CSV.read(File.join(MIGRATION, name), headers: true).select do |row|
    owners.include?(row["OwnerTask"])
  end
end

def legacy_consumer(row)
  File.basename(row["Source"], ".cs")
end

def normalized_event_names(name)
  values = [name]
  values << name.sub(/Event\z/, "")
  values << name.sub(/Requested\z/, "")
  values << name.sub(/RequestEvent\z/, "Request")
  values.uniq.reject(&:empty?)
end

def mapped?(row, document, type_column)
  return true if document.include?(row["Key"])

  type = row[type_column]
  unless type_column == "EventType"
    return true if document.include?(type)
    if row["OwnerTask"] == "ECS-043" && row["Source"].start_with?("ECS/Objects/Temperance/")
      effect_source = Dir.glob(File.join(ROOT, "ECS", "DataOriented", "Gameplay", "Effects", "*.cs"))
        .sort.map { |path| File.read(path, encoding: "UTF-8").scrub }.join("\n")
      return effect_source.match?(/\b#{Regexp.escape(type)}\b/) && document.include?("Folded object behaviors")
    end
    if row["OwnerTask"] == "ECS-044"
      meta_source = Dir.glob(File.join(ROOT, "ECS", "DataOriented", "Gameplay", "Meta", "*.cs"))
        .sort.map { |path| File.read(path, encoding: "UTF-8").scrub }.join("\n")
      return meta_source.match?(/\b#{Regexp.escape(type)}\b/) &&
        (document.include?("Same-named") || document.include?("GeneratedMetaObjectCatalog"))
    end
    return false
  end

  consumer = legacy_consumer(row)
  # Subscription owner documents use one audited/count-checked row per legacy consumer.
  # The CSV remains the exact per-event key inventory, so the consumer row is sufficient
  # evidence without copying hundreds of subscription keys into prose.
  return true if document.include?(consumer)
  row["OwnerTask"] == "ECS-044" && row["Source"].start_with?("ECS/Objects/Achievements/") &&
    document.include?("Nineteen achievement definitions")
end

def enclosing_type(source, offset)
  declarations = []
  source.to_enum(:scan, /\b(?:(abstract|sealed)\s+)?class\s+(\w+)(?:\s*<[^>{}]+>)?[^\{]*\{/m).each do
    match = Regexp.last_match
    next if match.begin(0) > offset
    declarations << [match.begin(0), match[1], match[2]]
  end
  declarations.max_by(&:first)
end

def no_op_updates(paths)
  result = []
  pattern = /public\s+(?:virtual\s+)?void\s+Update\s*\(\s*ref\s+SystemContext\s+\w+\s*\)\s*(?:=>\s*\{\s*\}\s*;?|\{\s*\})/m
  paths.each do |path|
    source = File.read(path, encoding: "UTF-8").scrub
    source.to_enum(:scan, pattern).each do
      match = Regexp.last_match
      declaration = enclosing_type(source, match.begin(0))
      next unless declaration
      result << {
        source: relative(path),
        line: source[0...match.begin(0)].count("\n") + 1,
        abstract: declaration[1] == "abstract",
        system: declaration[2]
      }
    end
  end
  result
end

def type_bodies(paths)
  bodies = {}
  declaration = /\b(?:(?:abstract|sealed)\s+)?class\s+(\w+)(?:\s*<[^>{}]+>)?[^\{]*\{/m
  paths.each do |path|
    source = File.read(path, encoding: "UTF-8").scrub
    source.to_enum(:scan, declaration).each do
      match = Regexp.last_match
      opening = source.index("{", match.begin(0))
      next unless opening
      depth = 0
      closing = nil
      source[opening..].chars.each_with_index do |character, index|
        depth += 1 if character == "{"
        depth -= 1 if character == "}"
        if depth.zero?
          closing = opening + index
          break
        end
      end
      next unless closing
      bodies[match[1]] = {
        source: relative(path),
        line: source[0...match.begin(0)].count("\n") + 1,
        body: source[opening..closing]
      }
    end
  end
  bodies
end

def private_event_runtime_findings(paths)
  allowed = %w[
    ECS/DataOriented/Systems/SystemScheduler.cs
    ECS/DataOriented/Events/World.Events.cs
    ECS/DataOriented/Integration/DataOrientedGameRuntime.cs
  ]
  paths.flat_map do |path|
    next [] if allowed.include?(relative(path))
    line_findings([path], /\bnew\s+EventRuntime\s*\(|\.AttachEventRuntime\s*\(|public\s+EventRuntime\s+Attach\s*\(/)
  end
end

def service_mutations(paths)
  mutation = /EventManager\.(?:Publish|Subscribe|Unsubscribe)|\b(?:StateSingleton|Game1)\.\w+\s*=|\.GetComponent\s*</
  member_write = /\b[a-z_]\w*\.\w+\s*(?:=|\+=|-=|\*=|\/=|\+\+|--)|\b[a-z_]\w*\.(?:Add|Remove|Clear|Enqueue|Dequeue|Push|Pop)\s*\(/
  line_findings(paths, Regexp.union(mutation, member_write))
end

def draw_state_mutations(paths)
  findings = []
  method = /\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:void|bool|int|float|Vector\w*)\s+Draw\w*\s*\([^)]*\)\s*\{/m
  mutation = /EventManager\.(?:Publish|Subscribe|Unsubscribe)|\.GetComponent\s*<|\b(?:Owner|StateSingleton|Game1)\.\w+\s*=|\b(?:this\.)?\w+\.\w+\s*(?:=|\+=|-=|\*=|\/=|\+\+|--)|\.(?:Add|Remove|Clear|Enqueue|Dequeue)\s*\(/
  paths.each do |path|
    source = File.read(path, encoding: "UTF-8").scrub
    source.to_enum(:scan, method).each do
      match = Regexp.last_match
      opening = source.index("{", match.begin(0))
      next unless opening
      depth = 0
      closing = nil
      source[opening..].chars.each_with_index do |character, index|
        depth += 1 if character == "{"
        depth -= 1 if character == "}"
        if depth.zero?
          closing = opening + index
          break
        end
      end
      next unless closing
      body = source[opening..closing]
      base_line = source[0...opening].count("\n") + 1
      body.lines.each_with_index do |line, index|
        next unless line.match?(mutation)
        next if line.lstrip.start_with?("//")
        findings << LineFinding.new(source: relative(path), line: base_line + index, text: line.strip)
      end
    end
  end
  findings.uniq { |finding| [finding.source, finding.line, finding.text] }
end

owners = DOMAIN_DOCUMENTS.keys
documents = DOMAIN_DOCUMENTS.transform_values do |name|
  path = File.join(MIGRATION, name)
  raise "Missing mapping document #{relative(path)}" unless File.file?(path)
  File.read(path, encoding: "UTF-8").scrub
end

ledger_specs = {
  "components.csv" => "LegacyType",
  "events.csv" => "LegacyType",
  "systems.csv" => "LegacyType",
  "event-subscriptions.csv" => "EventType",
  "object-behaviors.csv" => "LegacyType"
}

unmapped = []
ledger_specs.each do |name, type_column|
  ledger_rows(name, owners).each do |row|
    document = documents.fetch(row["OwnerTask"])
    unmapped << [name, row] unless mapped?(row, document, type_column)
  end
end

new_paths = cs_files("ECS/DataOriented")
new_source_violations = NEW_SOURCE_PATTERNS.flat_map do |name, pattern|
  line_findings(new_paths, pattern).map { |finding| [name, finding] }
end
private_runtimes = private_event_runtime_findings(new_paths)

no_ops = no_op_updates(new_paths)
operational_no_ops = no_ops.each_with_object([]) do |finding, values|
  owner = OPERATIONAL_SYSTEMS.find { |_task, names| names.include?(finding[:system]) }&.first
  values << finding.merge(owner: owner) if owner
end
new_type_bodies = type_bodies(new_paths)
access_tokens = /\b(?:readComponents|writeComponents|readDynamicBufferTypes|writeDynamicBufferTypes|consumedEventTypeIds|emittedEventTypeIds|requiresExclusiveWorldAccess)\s*:|\b(?:reads|writes)\s*,/
empty_access_metadata = OPERATIONAL_SYSTEMS.flat_map do |owner, names|
  names.each_with_object([]) do |name, findings|
    declaration = new_type_bodies[name]
    next unless declaration
    next if declaration[:body].match?(access_tokens)
    findings << declaration.merge(owner: owner, system: name)
  end
end

legacy_paths = cs_files("ECS", exclude_data_oriented: true)
legacy_inventory = LEGACY_PATTERNS.transform_values { |pattern| line_findings(legacy_paths, pattern) }
legacy_inventory["service mutations"] = service_mutations(cs_files("ECS/Services"))
legacy_inventory["draw-state mutations"] = draw_state_mutations(legacy_paths)

system_rows = ledger_rows("systems.csv", owners)
subscription_rows = ledger_rows("event-subscriptions.csv", owners)

blockers = []
unmapped.each { |name, row| blockers << "unmapped ledger key: #{name}:#{row['Key']}" }
new_source_violations.each do |name, finding|
  blockers << "#{name}: #{finding.source}:#{finding.line}"
end
private_runtimes.each do |finding|
  blockers << "private EventRuntime attachment: #{finding.source}:#{finding.line}"
end
operational_no_ops.each do |finding|
  blockers << "no-op scheduled system: #{finding[:system]} (#{finding[:owner]}) at #{finding[:source]}:#{finding[:line]}"
end
empty_access_metadata.each do |finding|
  blockers << "empty scheduled access metadata: #{finding[:system]} (#{finding[:owner]}) at #{finding[:source]}:#{finding[:line]}"
end

summary = []
summary << "ECS-046 completeness pre-audit"
summary << "owners=#{owners.join(',')}"
summary << "ledger_rows=#{ledger_specs.sum { |name, _| ledger_rows(name, owners).length }}"
summary << "mapped_systems=#{system_rows.length - unmapped.count { |name, _| name == 'systems.csv' }}/#{system_rows.length}"
summary << "mapped_subscriptions=#{subscription_rows.length - unmapped.count { |name, _| name == 'event-subscriptions.csv' }}/#{subscription_rows.length}"
summary << "blockers=#{blockers.length}"
legacy_inventory.each { |name, findings| summary << "legacy_#{name.tr(' ', '_')}=#{findings.length}" }
puts summary.join("\n")
blockers.each { |blocker| warn "BLOCKER: #{blocker}" }

if options[:write]
  output = File.expand_path(options[:write], ROOT)
  lines = []
  lines << "# ECS-046 completeness pre-audit findings"
  lines << ""
  lines << "Generated by `scripts/audit-data-oriented-ecs-completeness.rb --write`. Do not edit this file by hand."
  lines << "Legacy findings are conservative static candidates and a cutover/deletion inventory, not blockers when their ledger responsibility is mapped."
  lines << ""
  lines << "## Summary"
  lines << ""
  summary.each { |value| lines << "- `#{value}`" }
  lines << ""
  lines << "## Blocking findings"
  lines << ""
  if blockers.empty?
    lines << "None for the currently completed domains."
  else
    blockers.each { |blocker| lines << "- #{blocker}" }
  end
  lines << ""
  lines << "## Explicit operational scheduler allowlist"
  lines << ""
  OPERATIONAL_SYSTEMS.each do |owner, names|
    names.each do |name|
      no_op = operational_no_ops.any? { |finding| finding[:owner] == owner && finding[:system] == name }
      empty_access = empty_access_metadata.any? { |finding| finding[:owner] == owner && finding[:system] == name }
      status = if no_op
        "BLOCKED: empty Update"
      elsif empty_access
        "BLOCKED: empty access metadata"
      else
        "operational owner"
      end
      lines << "- `#{owner}` | `#{name}` | #{status}"
    end
  end
  lines << ""
  lines << "All other legacy system ledger identities are unscheduled compatibility names, event/API owners,"
  lines << "consolidated responsibilities, or presentation/external adapter mappings described by the owner document."
  lines << ""
  lines << "## Legacy system disposition inventory"
  lines << ""
  lines << "| Ledger key | Owner | Mapping evidence |"
  lines << "| --- | --- | --- |"
  system_rows.each do |row|
    document = documents.fetch(row["OwnerTask"])
    evidence = document.lines.find { |line| line.include?(row["Key"]) || line.include?(row["LegacyType"]) }&.strip || "UNMAPPED"
    evidence = evidence.gsub("|", "\\|")
    lines << "| `#{row['Key']}` | `#{row['OwnerTask']}` | #{evidence} |"
  end
  lines << ""
  lines << "## Legacy event subscription inventory"
  lines << ""
  subscription_rows.each do |row|
    status = unmapped.any? { |name, candidate| name == "event-subscriptions.csv" && candidate["Key"] == row["Key"] } ? "UNMAPPED" : "mapped"
    lines << "- `#{row['Key']}` | `#{row['OwnerTask']}` | #{status}"
  end
  lines << ""
  legacy_inventory.each do |name, findings|
    lines << "## Legacy #{name} (#{findings.length})"
    lines << ""
    findings.each { |finding| lines << "- `#{finding.source}:#{finding.line}` — `#{finding.text.gsub('`', "'")}`" }
    lines << ""
  end
  File.write(output, lines.join("\n") + "\n")
  puts "wrote=#{relative(output)}"
end

exit 1 if options[:strict] && !blockers.empty?
