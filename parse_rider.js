const fs = require('fs');
const sarif = JSON.parse(fs.readFileSync('rider-inspections.sarif', 'utf8'));
const results = sarif.runs[0].results;
const grouped = {};
for (const r of results) {
  const rule = r.ruleId;
  const loc = r.locations[0].physicalLocation;
  const file = loc.artifactLocation.uri;
  const line = loc.region.startLine;
  const msg = r.message.text;
  if (!grouped[rule]) grouped[rule] = [];
  grouped[rule].push({ file, line, msg });
}
for (const [rule, items] of Object.entries(grouped)) {
  console.log('\n=== ' + rule + ' (' + items.length + ') ===');
  for (const i of items) console.log('  ' + i.file + ':' + i.line + ' | ' + i.msg);
}
