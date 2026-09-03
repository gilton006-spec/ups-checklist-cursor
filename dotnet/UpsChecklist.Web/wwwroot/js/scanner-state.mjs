// Pure state/date functions shared by the page and dependency-free Node tests.
export function versionForDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith('0000')) return null;
  const date = new Date(`${value}T12:00:00Z`);
  if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== value) return null;
  const day = date.getUTCDay();
  return day === 1 ? 'monday' : day >= 2 && day <= 5 ? 'tuesday-friday' : null;
}

export function localDate(now = new Date()) {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

export function createDraftStore(initial) {
  const drafts = initial instanceof Map ? initial : new Map();
  const key = (date, versionId, sheetId) => `${date}|${versionId}|${sheetId}`;
  return {
    get(date, versionId, sheetId) {
      const id = key(date, versionId, sheetId);
      if (!drafts.has(id)) drafts.set(id, {});
      return drafts.get(id);
    },
    hasEntries() {
      return [...drafts.values()].some(entries => Object.values(entries)
        .some(entry => Object.values(entry).some(value => String(value || '').trim().length > 0)));
    },
    exportMap() {
      return drafts;
    },
    replaceAll(next) {
      drafts.clear();
      if (!(next instanceof Map)) return;
      for (const [id, entries] of next.entries()) drafts.set(id, entries);
    },
  };
}

export function assignedCount(sheet, entries) {
  return sheet.rows.filter(row => entries[row.id]?.scannerNumber?.trim() && entries[row.id]?.handoverTo?.trim()).length;
}

export function sheetLabel(sheet) {
  return sheet.title.replace(/^Scanner list /i, '').replace(/^pd/i, 'PD');
}
