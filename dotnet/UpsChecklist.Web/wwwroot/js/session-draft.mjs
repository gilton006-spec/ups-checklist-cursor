/** Pure helpers for session-tab draft persistence (checklist + scanners). */

export const CHECKLIST_DRAFT_KEY = "ups-checklist:v1:jambreaker";
export const SCANNER_DRAFT_KEY = "ups-checklist:v1:scanners";
/** Discard restored drafts older than this (browser session restore can revive them). */
export const DRAFT_MAX_AGE_MS = 18 * 60 * 60 * 1000;

export function isDraftWithinAge(savedAt, nowMs = Date.now(), maxAgeMs = DRAFT_MAX_AGE_MS) {
  if (!savedAt) return false;
  const parsed = Date.parse(savedAt);
  if (Number.isNaN(parsed)) return false;
  return nowMs - parsed <= maxAgeMs;
}

export function draftHasContent(draft) {
  if (!draft || typeof draft !== "object") return false;
  return Object.values(draft.checks || {}).some(Boolean)
    || !!String(draft.count || "").trim()
    || !!String(draft.remarks || "").trim()
    || !!String(draft.beforeSortEvidencePhoto || "").trim()
    || !!String(draft.evidencePhoto || "").trim()
    || !!String(draft.signature || "").trim()
    || !!String(draft.drawn || "").trim()
    || Object.values(draft.sectionRemarks || {}).some((value) => String(value || "").trim());
}

export function checklistStateHasContent(snapshot) {
  if (!snapshot || typeof snapshot !== "object") return false;
  if (String(snapshot.name || "").trim()) return true;
  return Object.values(snapshot.drafts || {}).some(draftHasContent);
}

export function cloneChecklistSnapshot(state) {
  return {
    v: 1,
    savedAt: new Date().toISOString(),
    positionId: state.positionId || "",
    pickerOpen: state.pickerOpen !== false,
    name: state.name || "",
    date: state.date || "",
    signatureMode: state.signatureMode || "type",
    drafts: structuredClone(state.drafts || {}),
    photosIncluded: true,
  };
}

export function stripHeavyMedia(snapshot) {
  const next = structuredClone(snapshot);
  next.photosIncluded = false;
  next.photosOmitted = true;
  for (const draft of Object.values(next.drafts || {})) {
    draft.beforeSortEvidencePhoto = "";
    draft.evidencePhoto = "";
  }
  return next;
}

export function stripDrawnSignatures(snapshot) {
  const next = structuredClone(snapshot);
  next.drawnOmitted = true;
  for (const draft of Object.values(next.drafts || {})) {
    draft.drawn = "";
  }
  return next;
}

/**
 * Builds save candidates from heaviest to lightest for quota fallback.
 * @returns {object[]}
 */
export function checklistSaveCandidates(state) {
  const full = cloneChecklistSnapshot(state);
  if (!checklistStateHasContent(full)) return [];
  const withoutPhotos = stripHeavyMedia(full);
  const withoutDrawn = stripDrawnSignatures(withoutPhotos);
  return [full, withoutPhotos, withoutDrawn];
}

export function parseChecklistSnapshot(raw, knownPositionIds = null) {
  if (!raw) return null;
  let data;
  try {
    data = typeof raw === "string" ? JSON.parse(raw) : raw;
  } catch {
    return null;
  }
  if (!data || data.v !== 1 || typeof data.drafts !== "object" || data.drafts === null) return null;

  const drafts = {};
  for (const [id, draft] of Object.entries(data.drafts)) {
    if (knownPositionIds && !knownPositionIds.has(id)) continue;
    if (!draft || typeof draft !== "object") continue;
    drafts[id] = {
      checks: typeof draft.checks === "object" && draft.checks ? { ...draft.checks } : {},
      count: String(draft.count || "").replace(/\D/g, "").slice(0, 5),
      remarks: String(draft.remarks || "").slice(0, 2000),
      sectionRemarks: typeof draft.sectionRemarks === "object" && draft.sectionRemarks
        ? { ...draft.sectionRemarks } : {},
      beforeSortEvidencePhoto: String(draft.beforeSortEvidencePhoto || ""),
      evidencePhoto: String(draft.evidencePhoto || ""),
      signature: String(draft.signature || "").slice(0, 65),
      drawn: String(draft.drawn || ""),
      signatureMode: draft.signatureMode === "draw" ? "draw" : "type",
    };
  }

  const positionId = knownPositionIds && data.positionId && knownPositionIds.has(data.positionId)
    ? data.positionId
    : (data.positionId && drafts[data.positionId] ? data.positionId : "");

  return {
    v: 1,
    savedAt: typeof data.savedAt === "string" ? data.savedAt : "",
    positionId,
    pickerOpen: data.pickerOpen !== false,
    name: String(data.name || "").slice(0, 65),
    date: /^\d{4}-\d{2}-\d{2}$/.test(data.date || "") ? data.date : "",
    signatureMode: data.signatureMode === "draw" ? "draw" : "type",
    drafts,
    photosIncluded: data.photosIncluded !== false && !data.photosOmitted,
    photosOmitted: !!data.photosOmitted,
    drawnOmitted: !!data.drawnOmitted,
  };
}

export function serializeScannerStore(mapLike, selection = {}) {
  const drafts = {};
  for (const [key, entries] of mapLike.entries()) {
    // Skip empty sheets so restore stays small and meaningful.
    const hasContent = Object.values(entries || {}).some((entry) =>
      Object.values(entry || {}).some((value) => String(value || "").trim()));
    if (!hasContent) continue;
    drafts[key] = structuredClone(entries);
  }
  return {
    v: 1,
    savedAt: new Date().toISOString(),
    date: typeof selection.date === "string" ? selection.date : "",
    sheetId: typeof selection.sheetId === "string" ? selection.sheetId : "",
    drafts,
  };
}

export function parseScannerStore(raw) {
  if (!raw) return null;
  let data;
  try {
    data = typeof raw === "string" ? JSON.parse(raw) : raw;
  } catch {
    return null;
  }
  if (!data || data.v !== 1 || typeof data.drafts !== "object" || data.drafts === null) return null;
  const drafts = new Map();
  for (const [key, entries] of Object.entries(data.drafts)) {
    if (typeof entries !== "object" || !entries) continue;
    const clean = {};
    for (const [rowId, entry] of Object.entries(entries)) {
      if (!entry || typeof entry !== "object") continue;
      clean[rowId] = {
        handoverTo: String(entry.handoverTo || ""),
        scannerNumber: String(entry.scannerNumber || ""),
        comments: String(entry.comments || ""),
        position: String(entry.position || ""),
      };
    }
    if (!Object.keys(clean).length) continue;
    drafts.set(key, clean);
  }
  return {
    v: 1,
    savedAt: typeof data.savedAt === "string" ? data.savedAt : "",
    date: /^\d{4}-\d{2}-\d{2}$/.test(data.date || "") ? data.date : "",
    sheetId: typeof data.sheetId === "string" ? data.sheetId : "",
    drafts,
  };
}

/** Tries candidates in order until one fits sessionStorage. */
export function writeSessionCandidates(storage, key, candidates) {
  if (!storage || !candidates.length) {
    try { storage?.removeItem(key); } catch { /* ignore */ }
    return { ok: true, omitted: false };
  }
  let lastError = null;
  for (let i = 0; i < candidates.length; i += 1) {
    try {
      storage.setItem(key, JSON.stringify(candidates[i]));
      return {
        ok: true,
        omitted: i > 0,
        photosOmitted: !!candidates[i].photosOmitted,
        drawnOmitted: !!candidates[i].drawnOmitted,
      };
    } catch (error) {
      lastError = error;
    }
  }
  return { ok: false, error: lastError };
}

export function readSessionJson(storage, key) {
  try {
    return storage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

export function clearSessionKey(storage, key) {
  try { storage?.removeItem(key); } catch { /* ignore */ }
}
