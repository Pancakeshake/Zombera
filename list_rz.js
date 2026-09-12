const d = JSON.parse(require('fs').readFileSync('rider_issues.json', 'utf8'));
const allIssues = [];
for (const [rule, items] of Object.entries(d)) {
  for (const i of items) {
    allIssues.push({ rule, uri: i.uri, line: i.line, msg: i.msg });
  }
}
// Files R-Z
const rz = allIssues.filter(i => {
  const f = i.uri.split('/').pop();
  return f >= 'R' && f <= 'z';
});
const byFile = {};
for (const i of rz) {
  if (!byFile[i.uri]) byFile[i.uri] = [];
  byFile[i.uri].push({ rule: i.rule, line: i.line, msg: i.msg.substring(0, 80) });
}
const sorted = Object.keys(byFile).sort();
for (const f of sorted) {
  process.stdout.write('\n=== ' + f + ' ===\n');
  byFile[f].sort((a, b) => a.line - b.line).forEach(i =>
    process.stdout.write('  L' + i.line + ' [' + i.rule + '] ' + i.msg + '\n')
  );
}
