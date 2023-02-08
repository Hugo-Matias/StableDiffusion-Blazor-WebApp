export function init(image) {
  function loadImage(data) {
    image.src = data;
  }

  return {
    loadImage,
  };
}
