import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { versionForDate, localDate, createDraftStore, assignedCount, sheetLabel } from '../../UpsChecklist.Web/wwwroot/js/scanner-state.mjs';

test('Monday and Tuesday-Friday chosen from the calendar date, independent of timezone', () => {
  for (const [date, expected] of [['2026-09-07', 'monday'], ['2026-09-08', 'tuesday-friday'],
    ['2026-09-09', 'tuesday-friday'], ['2026-09-10', 'tuesday-friday'], ['2026-09-11', 'tuesday-friday'],
    ['2026-09-12', null], ['2026-09-13', null], ['2026-02-30', null], ['', null], ['bad', null], ['0000-01-01', null]]) {
    assert.equal(versionForDate(date), expected, date);
  }
  assert.equal(localDate(new Date(2026, 8, 7, 23, 30)), '2026-09-07');
});
test('drafts survive list changes but never leak across dates, versions or lists', () => {
  const store = createDraftStore();
  assert.equal(store.hasEntries(), false);
  const monday = store.get('2026-09-07', 'monday', 'pd1');
  monday.r5 = { scannerNumber: 'SC-12', handoverTo: 'QA Test' };
  assert.equal(store.hasEntries(), true);
  for (const args of [['2026-09-07', 'monday', 'pd2'], ['2026-09-08', 'tuesday-friday', 'pd1'], ['2026-09-14', 'monday', 'pd1']]) {
    assert.deepEqual(store.get(...args), {});
  }
  assert.equal(store.get('2026-09-07', 'monday', 'pd1').r5.scannerNumber, 'SC-12');
  const clone = createDraftStore();
  clone.replaceAll(store.exportMap());
  assert.equal(clone.get('2026-09-07', 'monday', 'pd1').r5.handoverTo, 'QA Test');
});
test('progress counts assignments, not merely edited or whitespace-only rows', () => {
  const sheet = { rows: [{ id: 'r5' }, { id: 'r7' }, { id: 'r9' }] };
  assert.equal(assignedCount(sheet, { r5: { scannerNumber: '12', handoverTo: 'QA' },
    r7: { scannerNumber: '13' }, r9: { scannerNumber: ' ', handoverTo: 'QA' } }), 1);
});
test('all source lists remain selectable, including Monday PD1 part 2', () => {
  const catalog = JSON.parse(readFileSync(new URL('../../UpsChecklist.Core/Data/scanner-lists.json', import.meta.url)));
  assert.equal(catalog.length, 2);
  assert.equal(catalog.flatMap(v => v.sheets).flatMap(s => s.rows).length, 80);
  assert.deepEqual(catalog[0].sheets.map(sheetLabel), ['Inbound', 'PD1 1 of 2', 'PD1 2 of 2', 'PD3', 'PD4']);
  assert.deepEqual(catalog[1].sheets.map(sheetLabel), ['Inbound', 'PD1', 'PD2', 'PD3', 'PD4']);
  assert.equal(catalog[1].sheets.find(s => s.id === 'pd2').rows.find(r => r.userName === 'Stockx 03').position, '73 / 74');
  assert.equal(catalog[1].sheets.find(s => s.id === 'pd2').rows.find(r => r.userName === 'Repack Future').scannerType, 'Two piece');
  assert.equal(catalog[0].sheets.find(s => s.id === 'pd2').rows.find(r => r.userName === 'Repack Future').scannerType, 'One piece');
});
