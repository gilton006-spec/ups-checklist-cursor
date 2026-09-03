import test from 'node:test';
import assert from 'node:assert/strict';
import {
  CHECKLIST_DRAFT_KEY,
  SCANNER_DRAFT_KEY,
  checklistSaveCandidates,
  checklistStateHasContent,
  clearSessionKey,
  cloneChecklistSnapshot,
  draftHasContent,
  isDraftWithinAge,
  parseChecklistSnapshot,
  parseScannerStore,
  readSessionJson,
  serializeScannerStore,
  stripDrawnSignatures,
  stripHeavyMedia,
  writeSessionCandidates,
} from '../../UpsChecklist.Web/wwwroot/js/session-draft.mjs';
import { createDraftStore } from '../../UpsChecklist.Web/wwwroot/js/scanner-state.mjs';

test('empty drafts are not treated as restorable content', () => {
  assert.equal(draftHasContent({
    checks: {}, count: '', remarks: '', sectionRemarks: {},
    beforeSortEvidencePhoto: '', evidencePhoto: '', signature: '', drawn: '',
  }), false);
  assert.equal(checklistStateHasContent({ name: '  ', drafts: {} }), false);
  assert.equal(checklistStateHasContent({
    name: '',
    drafts: { a: { checks: { i1: true }, count: '', remarks: '', sectionRemarks: {},
      beforeSortEvidencePhoto: '', evidencePhoto: '', signature: '', drawn: '' } },
  }), true);
});

test('parse restores known positions and drops unknown ones', () => {
  const raw = JSON.stringify({
    v: 1,
    name: 'QA Example',
    date: '2026-09-03',
    positionId: 'ghost',
    pickerOpen: false,
    signatureMode: 'draw',
    drafts: {
      ghost: { checks: { x: true }, count: '1', remarks: '', sectionRemarks: {},
        beforeSortEvidencePhoto: '', evidencePhoto: '', signature: '', drawn: '', signatureMode: 'draw' },
      real: { checks: { a: true }, count: '2', remarks: 'note', sectionRemarks: { sls1: 'ok' },
        beforeSortEvidencePhoto: 'data:image/jpeg;base64,abc', evidencePhoto: '',
        signature: 'QA', drawn: 'data:image/png;base64,xyz', signatureMode: 'type' },
    },
  });
  const parsed = parseChecklistSnapshot(raw, new Set(['real']));
  assert.equal(parsed.positionId, '');
  assert.equal(parsed.name, 'QA Example');
  assert.equal(parsed.date, '2026-09-03');
  assert.deepEqual(Object.keys(parsed.drafts), ['real']);
  assert.equal(parsed.drafts.real.count, '2');
  assert.equal(parsed.drafts.real.checks.a, true);
});

test('quota fallback strips photos then drawn signatures', () => {
  const state = {
    positionId: 'real',
    pickerOpen: false,
    name: 'QA',
    date: '2026-09-03',
    signatureMode: 'draw',
    drafts: {
      real: {
        checks: { a: true }, count: '1', remarks: '', sectionRemarks: {},
        beforeSortEvidencePhoto: 'photo-a', evidencePhoto: 'photo-b',
        signature: '', drawn: 'drawn-sig', signatureMode: 'draw',
      },
    },
  };
  const candidates = checklistSaveCandidates(state);
  assert.equal(candidates.length, 3);
  assert.equal(candidates[0].drafts.real.beforeSortEvidencePhoto, 'photo-a');
  assert.equal(candidates[1].photosOmitted, true);
  assert.equal(candidates[1].drafts.real.beforeSortEvidencePhoto, '');
  assert.equal(candidates[1].drafts.real.drawn, 'drawn-sig');
  assert.equal(candidates[2].drawnOmitted, true);
  assert.equal(candidates[2].drafts.real.drawn, '');
  assert.equal(stripHeavyMedia(cloneChecklistSnapshot(state)).photosOmitted, true);
  assert.equal(stripDrawnSignatures(cloneChecklistSnapshot(state)).drawnOmitted, true);
});

test('writeSessionCandidates retries lighter payloads after quota errors', () => {
  const writes = [];
  const storage = {
    setItem(key, value) {
      writes.push(key);
      if (writes.length === 1) throw new Error('QuotaExceededError');
      this.data = value;
    },
    removeItem() { this.data = null; },
    getItem() { return this.data; },
    data: null,
  };
  const result = writeSessionCandidates(storage, CHECKLIST_DRAFT_KEY, [
    { v: 1, photosIncluded: true, drafts: { a: { beforeSortEvidencePhoto: 'big' } } },
    { v: 1, photosOmitted: true, drafts: { a: { beforeSortEvidencePhoto: '' } } },
  ]);
  assert.equal(result.ok, true);
  assert.equal(result.omitted, true);
  assert.equal(result.photosOmitted, true);
  assert.match(storage.data, /photosOmitted/);
});

test('scanner store round-trips through session JSON without leaking empty maps', () => {
  const store = createDraftStore();
  const monday = store.get('2026-09-07', 'monday', 'pd1');
  monday.r5 = { scannerNumber: 'SC-12', handoverTo: 'QA Test', comments: '', position: '' };
  store.get('2026-09-07', 'monday', 'pd2'); // empty sheet should be omitted from save
  const packed = serializeScannerStore(store.exportMap(), { date: '2026-09-07', sheetId: 'pd1' });
  assert.equal(packed.v, 1);
  assert.equal(packed.date, '2026-09-07');
  assert.equal(packed.sheetId, 'pd1');
  assert.ok(packed.drafts['2026-09-07|monday|pd1']);
  assert.equal(packed.drafts['2026-09-07|monday|pd2'], undefined);
  const parsed = parseScannerStore(JSON.stringify(packed));
  assert.equal(parsed.date, '2026-09-07');
  assert.equal(parsed.sheetId, 'pd1');
  const restored = createDraftStore(parsed.drafts);
  assert.equal(restored.hasEntries(), true);
  assert.equal(restored.get('2026-09-07', 'monday', 'pd1').r5.scannerNumber, 'SC-12');
  assert.deepEqual(restored.get('2026-09-08', 'tuesday-friday', 'pd1'), {});
});

test('clearing removes the session key used by the pages', () => {
  const storage = {
    data: { [CHECKLIST_DRAFT_KEY]: 'x', [SCANNER_DRAFT_KEY]: 'y' },
    getItem(key) { return this.data[key] ?? null; },
    removeItem(key) { delete this.data[key]; },
  };
  clearSessionKey(storage, CHECKLIST_DRAFT_KEY);
  assert.equal(readSessionJson(storage, CHECKLIST_DRAFT_KEY), null);
  assert.equal(readSessionJson(storage, SCANNER_DRAFT_KEY), 'y');
});

test('draft age helper rejects missing and stale timestamps', () => {
  assert.equal(isDraftWithinAge(''), false);
  assert.equal(isDraftWithinAge('not-a-date'), false);
  const now = Date.parse('2026-09-03T12:00:00.000Z');
  assert.equal(isDraftWithinAge('2026-09-03T11:00:00.000Z', now), true);
  assert.equal(isDraftWithinAge('2026-09-02T12:00:00.000Z', now), false);
});
