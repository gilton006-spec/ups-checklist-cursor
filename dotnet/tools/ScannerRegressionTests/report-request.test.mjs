import test from 'node:test';
import assert from 'node:assert/strict';
import { ReportRequestError, antiforgeryHeaders, fetchWithTimeout } from '../../UpsChecklist.Web/wwwroot/js/report-request.mjs';

test('antiforgery headers are omitted when the page token is missing', () => {
  const doc = { querySelector: () => null };
  assert.deepEqual(antiforgeryHeaders(doc), {});
});

test('antiforgery headers copy the hidden token', () => {
  const doc = { querySelector: () => ({ value: 'token-1' }) };
  assert.deepEqual(antiforgeryHeaders(doc), { RequestVerificationToken: 'token-1' });
});

test('timeout errors stay distinguishable from user cancellation', () => {
  const timeout = new ReportRequestError('timed out', { timeout: true });
  const aborted = new ReportRequestError('cancelled', { aborted: true });
  assert.equal(timeout.timeout, true);
  assert.equal(aborted.aborted, true);
});

test('already-cancelled signal does not start fetch', async () => {
  const controller = new AbortController();
  controller.abort();
  let fetchCalled = false;
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => {
    fetchCalled = true;
    return new Response('ok');
  };
  try {
    await assert.rejects(
      () => fetchWithTimeout('https://example.test/report', { signal: controller.signal }, 1000),
      (error) => error instanceof ReportRequestError && error.aborted === true);
    assert.equal(fetchCalled, false);
  } finally {
    globalThis.fetch = originalFetch;
  }
});
