/** Restores a canvas image only if a newer restore has not started. */
export function createImageRestore(draw, loadImage = src => {
  const image = new Image();
  image.src = src;
  return image;
}) {
  let generation = 0;
  return function restore(value) {
    const mine = ++generation;
    if (!value) return;
    const image = loadImage(value);
    image.onload = () => {
      if (mine !== generation) return;
      draw(image);
    };
    image.onerror = () => {};
  };
}
