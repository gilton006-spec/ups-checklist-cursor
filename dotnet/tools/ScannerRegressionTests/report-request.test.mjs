import test from 'node:test';
import assert from 'node:assert/strict';
import { ReportRequestError, antiforgeryHeaders } from '../../UpsChecklist.Web/wwwroot/js/report-request.mjs';

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
