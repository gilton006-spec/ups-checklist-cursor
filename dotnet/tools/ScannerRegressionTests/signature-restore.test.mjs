import test from 'node:test';
import assert from 'node:assert/strict';
import { createImageRestore } from '../../UpsChecklist.Web/wwwroot/js/signature-restore.mjs';

test('a slower signature restore cannot redraw after a newer value is set', () => {
  const drawn = [];
  const loaders = [];
  const restore = createImageRestore((image) => drawn.push(image.id), (src) => {
    const image = { src, id: src };
    loaders.push(image);
    return image;
  });
  restore('first');
  restore('second');
  loaders[0].onload();
  loaders[1].onload();
  assert.deepEqual(drawn, ['second']);
});

test('clearing increments generation so a late image is ignored', () => {
  const drawn = [];
  const loaders = [];
  const restore = createImageRestore((image) => drawn.push(image.id), (src) => {
    const image = { src, id: src || 'empty' };
    loaders.push(image);
    return image;
  });
  restore('stale');
  restore('');
  loaders[0].onload();
  assert.deepEqual(drawn, []);
});
