const d = JSON.parse(require('fs').readFileSync('rider_issues.json', 'utf8'));
const allIssues = [];
for (const [rule, items] of Object.entries(d)) {
  for (const i of items) {
    const file = i.uri.split('/').pop();
    if (file && file[0] >= 'R' && file[0] <= 'Z') {
      allIssues.push({ rule, file, line: i.line, uri: i.uri, msg: i.msg });
    }
  }
}
allIssues.sort((a, b) => a.uri.localeCompare(b.uri) || a.line - b.line);
const byFile = {};
for (const i of allIssues) {
  if (!byFile[i.uri]) byFile[i.uri] = [];
  byFile[i.uri].push(i);
}
let out = '';
for (const [uri, items] of Object.entries(byFile)) {
  out += '\n=== ' + uri + ' ===\n';
  for (const i of items) out += '  L' + i.line + ' [' + i.rule + '] ' + i.msg.substring(0, 100) + '\n';
}
require('fs').writeFileSync('rz_issues.txt', out);
console.log('Files with R-Z issues:', Object.keys(byFile).length);
