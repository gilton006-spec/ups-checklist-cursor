import { versionForDate, localDate, createDraftStore, assignedCount, sheetLabel } from './scanner-state.mjs';
import {
  SCANNER_DRAFT_KEY,
  clearSessionKey,
  parseScannerStore,
  readSessionJson,
  serializeScannerStore,
  writeSessionCandidates,
} from './session-draft.mjs';

const catalog = JSON.parse(document.getElementById('scanner-source').textContent);
const byId = id => document.getElementById(id);
const date = byId('scanner-date');
const picker = byId('scanner-sheet');
const download = byId('scanner-download');
const status = byId('scanner-status');
const rows = byId('scanner-rows');
const drafts = createDraftStore();
let version, sheet, entries, busy = false;
let suppressStatusClear = false;
let restoreNotice = '';

function persistScannerDraftNow() {
  if (!drafts.hasEntries()) {
    clearSessionKey(sessionStorage, SCANNER_DRAFT_KEY);
    return;
  }
  writeSessionCandidates(sessionStorage, SCANNER_DRAFT_KEY, [serializeScannerStore(drafts.exportMap(), {
    date: date.value || '',
    sheetId: picker.value || sheet?.id || '',
  })]);
}

function restoreScannerDraft() {
  const parsed = parseScannerStore(readSessionJson(sessionStorage, SCANNER_DRAFT_KEY));
  if (!parsed?.drafts?.size) return null;
  drafts.replaceAll(parsed.drafts);
  if (!drafts.hasEntries()) return null;
  return parsed;
}

function element(tag, text, className) {
  const node = document.createElement(tag);
  if (text) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function updateProgress() {
  byId('scanner-progress').textContent = sheet
    ? `${assignedCount(sheet, entries)} / ${sheet.rows.length} assigned` : '';
}

function bindInput(input, row, key) {
  input.value = entries[row.id]?.[key] || '';
  input.addEventListener('input', () => {
    entries[row.id] ??= { handoverTo: '', scannerNumber: '', comments: '', position: '' };
    entries[row.id][key] = input.value;
    status.textContent = '';
    updateProgress();
    // Scanners payloads are small; write immediately so mobile refresh cannot miss a debounce.
    persistScannerDraftNow();
  });
}

function textCell(value, className) {
  const td = element('td', value, className);
  return td;
}

function inputCell(row, key, label, maxLength, extraClass) {
  const td = element('td', '', extraClass || '');
  const input = document.createElement('input');
  input.id = `${row.id}-${key}`;
  input.name = input.id;
  input.maxLength = maxLength;
  input.setAttribute('aria-label', `${row.userName}: ${label}`);
  bindInput(input, row, key);
  td.append(input);
  return td;
}

function commentsCell(row) {
  const td = element('td', '', 'scanner-comments-cell');
  if (row.note) td.append(element('div', row.note, 'scanner-printed-note'));
  const input = document.createElement('textarea');
  input.id = `${row.id}-comments`;
  input.name = input.id;
  input.maxLength = 300;
  input.rows = 1;
  input.setAttribute('aria-label', `${row.userName}: Comments`);
  bindInput(input, row, 'comments');
  td.append(input);
  return td;
}

function showSheet() {
  sheet = version?.sheets.find(s => s.id === picker.value);
  rows.replaceChildren();
  byId('scanner-list-panel').hidden = !sheet;
  download.disabled = busy || !sheet;
  if (!suppressStatusClear) status.textContent = '';
  const day = byId('scanner-day-label');
  if (!sheet) {
    byId('scanner-list-title').textContent = '';
    day.textContent = '';
    day.removeAttribute('data-day');
    updateProgress();
    return;
  }
  entries = drafts.get(date.value, version.id, sheet.id);
  byId('scanner-list-title').textContent = sheet.title;
  day.textContent = version.label;
  day.dataset.day = version.id;

  const table = element('table', '', 'scanner-sheet');
  table.setAttribute('aria-labelledby', 'scanner-list-title');
  const head = document.createElement('thead');
  const headerRow = document.createElement('tr');
  for (const header of sheet.headers) headerRow.append(element('th', header));
  head.append(headerRow);
  table.append(head);

  const body = document.createElement('tbody');
  for (const row of sheet.rows) {
    const tr = document.createElement('tr');
    const cells = [
      textCell(row.userName, 'scanner-name'),
      textCell(row.gost),
      textCell(row.scannerType, row.scannerType === 'Two piece' ? 'scanner-two-piece' : ''),
      row.position ? textCell(row.position) : inputCell(row, 'position', 'Position', 40),
      inputCell(row, 'handoverTo', 'Handover to', 80),
      inputCell(row, 'scannerNumber', 'Scanner#', 40),
      commentsCell(row),
    ];
    // On a phone the table stacks, so each cell carries its column name.
    cells.forEach((td, column) => {
      td.dataset.label = sheet.headers[column];
      tr.append(td);
    });
    body.append(tr);
  }
  for (const instruction of sheet.instructions) {
    const tr = element('tr', '', 'scanner-excel-line');
    const td = document.createElement('td');
    td.colSpan = sheet.headers.length;
    td.textContent = instruction.text;
    tr.append(td);
    body.append(tr);
  }
  table.append(body);
  rows.append(table);
  updateProgress();
  persistScannerDraftNow();
}

function chooseDate() {
  const previous = picker.value;
  version = catalog.find(v => v.id === versionForDate(date.value));
  picker.replaceChildren();
  picker.disabled = !version;
  byId('scanner-version').textContent = version ? version.label : '';
  const unavailable = byId('scanner-unavailable');
  unavailable.hidden = !!version;
  unavailable.textContent = date.value
    ? 'No scanner list is supplied for this date. Choose a weekday.' : 'Choose a date.';
  if (version) {
    for (const s of version.sheets) {
      const option = element('option', sheetLabel(s));
      option.value = s.id;
      picker.append(option);
    }
    if (version.sheets.some(s => s.id === previous)) picker.value = previous;
  }
  showSheet();
}

const restored = restoreScannerDraft();
if (restored?.date) date.value = restored.date;
else date.value = localDate();

date.addEventListener('change', () => {
  chooseDate();
  persistScannerDraftNow();
});
picker.addEventListener('change', () => {
  showSheet();
  persistScannerDraftNow();
});

suppressStatusClear = !!restored;
chooseDate();
if (restored?.sheetId && version?.sheets.some(s => s.id === restored.sheetId)) {
  picker.value = restored.sheetId;
  showSheet();
}
suppressStatusClear = false;
if (restored) {
  restoreNotice = 'Restored draft from this browser tab.';
  status.textContent = restoreNotice;
}

byId('scanner-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!sheet || busy) return;
  persistScannerDraftNow();
  busy = true;
  download.disabled = true;
  date.disabled = picker.disabled = true;
  rows.querySelectorAll('input, textarea').forEach(input => { input.disabled = true; });
  byId('scanner-form').setAttribute('aria-busy', 'true');
  download.textContent = 'Creating PDF…';
  status.textContent = '';
  const payload = JSON.stringify({ date: date.value, versionId: version.id, sheetId: sheet.id, entries });
  const filename = `UPS_scanners_${version.id}_${sheet.id}_${date.value}.pdf`;
  try {
    const response = await fetch('/api/scanners/download', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value,
      },
      body: payload,
    });
    if (!response.ok) throw new Error(await response.text());
    if (!response.headers.get('Content-Type')?.includes('application/pdf')) throw new Error('The server did not return a PDF.');
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
    status.textContent = 'PDF ready. Check your downloads and hand it over to your team leader. It has not been sent.';
  } catch (error) {
    status.textContent = `${error.message || 'The PDF could not be created.'} Your entries are still on this page.`;
  } finally {
    busy = false;
    download.disabled = false;
    date.disabled = picker.disabled = false;
    rows.querySelectorAll('input, textarea').forEach(input => { input.disabled = false; });
    byId('scanner-form').removeAttribute('aria-busy');
    download.textContent = 'Download PDF';
  }
});

function flushDraftOnLeave() {
  persistScannerDraftNow();
}

window.addEventListener('beforeunload', event => {
  flushDraftOnLeave();
  if (!drafts.hasEntries()) return;
  event.preventDefault();
  event.returnValue = '';
});
window.addEventListener('pagehide', flushDraftOnLeave);
document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'hidden') flushDraftOnLeave();
});
