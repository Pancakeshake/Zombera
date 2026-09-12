const sarif = JSON.parse(require('fs').readFileSync('rider-inspections.sarif', 'utf8'));
const results = sarif.runs[0].results;
const skip = new Set([
  'SuggestVarOrType_BuiltInTypes','SuggestVarOrType_Elsewhere','SuggestVarOrType_SimpleTypes',
  'Unity.PerformanceCriticalCodeInvocation','Unity.PerformanceCriticalCodeNullComparison','Unity.PerformanceCriticalCodeCameraMain'
]);
const byFile = {};
for (const r of results) {
  if (skip.has(r.ruleId)) continue;
  const loc = r.locations && r.locations[0];
  const uri = (loc && loc.physicalLocation && loc.physicalLocation.artifactLocation && loc.physicalLocation.artifactLocation.uri) || '';
  const fname = uri.split('/').pop();
  if (fname < 'N') continue;
  const line = (loc && loc.physicalLocation && loc.physicalLocation.region && loc.physicalLocation.region.startLine) || 0;
  const msg = (r.message && r.message.text) || '';
  const key = fname + '|' + uri;
  if (!byFile[key]) byFile[key] = [];
  byFile[key].push({ rule: r.ruleId, line, msg });
}
const sorted = Object.keys(byFile).sort();
const lines = [];
for (const k of sorted) {
  byFile[k].sort((a, b) => a.line - b.line);
  lines.push('\n=== ' + k + ' ===');
  for (const i of byFile[k]) {
    lines.push('  L' + i.line + ' [' + i.rule + '] ' + i.msg.substring(0, 100));
  }
}
require('fs').writeFileSync('issues_nz.txt', lines.join('\n'));
console.log('written');
