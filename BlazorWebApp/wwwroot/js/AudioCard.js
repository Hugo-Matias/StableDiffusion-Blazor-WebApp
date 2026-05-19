const cards = new Map();

export async function initialize(elementId, audioSource) {
  const root = document.getElementById(elementId);
  if (!root) return;

  const canvas = root.querySelector(".audio-waveform");
  const audio = root.querySelector(".audio-element");
  cards.set(elementId, { root, audio });

  if (!canvas || !audioSource) return;

  try {
    await drawWaveform(canvas, audioSource);
  } catch {
    drawFallback(canvas);
  }
}

export async function play(elementId) {
  const card = cards.get(elementId);
  if (!card?.audio) return;

  try {
    card.audio.currentTime = 0;
    card.audio.volume = 0.8;
    await card.audio.play();
  } catch {
    // Browser gesture policies can reject hover playback; the card still renders normally.
  }
}

export function pause(elementId) {
  const card = cards.get(elementId);
  if (!card?.audio) return;

  card.audio.pause();
}

export function dispose(elementId) {
  pause(elementId);
  cards.delete(elementId);
}

async function drawWaveform(canvas, audioSource) {
  const response = await fetch(audioSource);
  const buffer = await response.arrayBuffer();
  const audioContext = new (window.AudioContext || window.webkitAudioContext)();
  const decoded = await audioContext.decodeAudioData(buffer.slice(0));
  const samples = decoded.getChannelData(0);

  const width = canvas.width;
  const height = canvas.height;
  const ctx = canvas.getContext("2d");
  const step = Math.ceil(samples.length / width);
  const middle = height / 2;

  ctx.clearRect(0, 0, width, height);
  paintBackdrop(ctx, width, height);

  const gradient = ctx.createLinearGradient(0, 0, width, 0);
  gradient.addColorStop(0, "#7dd3fc");
  gradient.addColorStop(0.45, "#facc15");
  gradient.addColorStop(1, "#f472b6");

  ctx.strokeStyle = gradient;
  ctx.lineWidth = 2;
  ctx.beginPath();

  for (let x = 0; x < width; x++) {
    let min = 1;
    let max = -1;
    const offset = x * step;
    for (let i = 0; i < step && offset + i < samples.length; i++) {
      const value = samples[offset + i];
      if (value < min) min = value;
      if (value > max) max = value;
    }

    ctx.moveTo(x, middle + min * middle * 0.75);
    ctx.lineTo(x, middle + max * middle * 0.75);
  }

  ctx.stroke();
  await audioContext.close();
}

function drawFallback(canvas) {
  const ctx = canvas.getContext("2d");
  const width = canvas.width;
  const height = canvas.height;
  const middle = height / 2;

  ctx.clearRect(0, 0, width, height);
  paintBackdrop(ctx, width, height);
  ctx.strokeStyle = "#7dd3fc";
  ctx.lineWidth = 2;
  ctx.beginPath();

  for (let x = 0; x < width; x += 4) {
    const amplitude = Math.sin(x * 0.08) * Math.cos(x * 0.021) * middle * 0.7;
    ctx.moveTo(x, middle - amplitude);
    ctx.lineTo(x, middle + amplitude);
  }

  ctx.stroke();
}

function paintBackdrop(ctx, width, height) {
  const background = ctx.createLinearGradient(0, 0, width, height);
  background.addColorStop(0, "#17212b");
  background.addColorStop(0.55, "#20312c");
  background.addColorStop(1, "#221a2d");
  ctx.fillStyle = background;
  ctx.fillRect(0, 0, width, height);
}
