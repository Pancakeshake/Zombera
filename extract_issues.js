const sarif = JSON.parse(require('fs').readFileSync('rider-inspections.sarif', 'utf8'));
const results = sarif.runs[0].results;
const skip = new Set([
  'SuggestVarOrType_BuiltInTypes',
  'SuggestVarOrType_Elsewhere',
  'SuggestVarOrType_SimpleTypes',
  'Unity.PerformanceCriticalCodeInvocation',
  'Unity.PerformanceCriticalCodeNullComparison',
  'Unity.PerformanceCriticalCodeCameraMain',
]);
const byRule = {};
for (const r of results) {
  if (skip.has(r.ruleId)) continue;
  const loc = r.locations && r.locations[0];
  const uri = (loc && loc.physicalLocation && loc.physicalLocation.artifactLocation && loc.physicalLocation.artifactLocation.uri) || '';
  const line = (loc && loc.physicalLocation && loc.physicalLocation.region && loc.physicalLocation.region.startLine) || 0;
  const msg = (r.message && r.message.text) || '';
  if (!byRule[r.ruleId]) {
    byRule[r.ruleId] = [];
  }
  byRule[r.ruleId].push({ uri, line, msg });
}
require('fs').writeFileSync('rider_issues.json', JSON.stringify(byRule, null, 2));
console.log('written', Object.keys(byRule).length, 'rules');
