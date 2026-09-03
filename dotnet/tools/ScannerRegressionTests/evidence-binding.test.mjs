import test from 'node:test';
import assert from 'node:assert/strict';
import { createEvidenceBinding } from '../../UpsChecklist.Web/wwwroot/js/evidence-binding.mjs';

function harness() {
  const drafts = {};
  const widgets = [];
  let busy = 0;
  let changes = 0;
  const binding = createEvidenceBinding({
    createWidget(root, options) {
      const widget = { root, options, value: '', cancelled: false, setValue(next) { this.value = next; }, cancel() { this.cancelled = true; } };
      widgets.push(widget);
      return widget;
    },
    draftFor(id) {
      drafts[id] ??= { beforeSortEvidencePhoto: '', evidencePhoto: '' };
      return drafts[id];
    },
    onValueChange: () => { changes += 1; },
    onBusyChange: count => { busy = count; },
  });
  const fields = ['beforeSortEvidencePhoto', 'evidencePhoto'];
  const mount = id => binding.mount(id, fields.map(field => ({ root: {}, field })));
  return { binding, drafts, widgets, mount, fields, busyCount: () => busy, changeCount: () => changes };
}

test('a photo that finishes after a position switch stays on the position that took it', () => {
  const h = harness();
  h.mount('pd3');
  const [before, after] = h.widgets;
  h.mount('pd4');

  before.options.onChange('data:image/jpeg;base64,PD3BEFORE');
  after.options.onChange('data:image/jpeg;base64,PD3AFTER');

  assert.equal(h.drafts.pd3.beforeSortEvidencePhoto, 'data:image/jpeg;base64,PD3BEFORE');
  assert.equal(h.drafts.pd3.evidencePhoto, 'data:image/jpeg;base64,PD3AFTER');
  assert.equal(h.drafts.pd4.beforeSortEvidencePhoto, '');
  assert.equal(h.drafts.pd4.evidencePhoto, '');
  assert.equal(h.changeCount(), 0, 'a replaced widget must not report the new position as edited');
});

test('replaced widgets are cancelled and cannot change the busy state', () => {
  const h = harness();
  h.mount('pd3');
  const stale = h.widgets[0];
  stale.options.onBusyChange(true);
  assert.equal(h.busyCount(), 1);

  h.mount('pd4');
  assert.equal(stale.cancelled, true);
  assert.equal(h.busyCount(), 0, 'switching position must not leave the export buttons disabled');

  stale.options.onBusyChange(false);
  stale.options.onBusyChange(true);
  assert.equal(h.busyCount(), 0);
});

test('busy counts both live photo widgets and clears when they finish', () => {
  const h = harness();
  h.mount('pd3');
  const [before, after] = h.widgets;
  before.options.onBusyChange(true);
  after.options.onBusyChange(true);
  assert.equal(h.busyCount(), 2);
  before.options.onBusyChange(false);
  assert.equal(h.busyCount(), 1);
  after.options.onBusyChange(false);
  assert.equal(h.busyCount(), 0);
});

test('returning to a position shows the photo it kept', () => {
  const h = harness();
  h.mount('pd3');
  h.widgets[0].options.onChange('data:image/jpeg;base64,KEPT');
  h.mount('pd4');
  h.mount('pd3');
  assert.equal(h.widgets.at(-2).value, 'data:image/jpeg;base64,KEPT');
  assert.equal(h.widgets.at(-1).value, '');
  assert.equal(h.changeCount(), 1);
});
