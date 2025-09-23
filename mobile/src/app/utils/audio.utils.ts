const TARGET_SAMPLE_RATE = 16000;

export interface EncodedFrame {
  payload: ArrayBuffer;
  samples: number;
}

export function downsampleTo16k(input: Float32Array, inputSampleRate: number): Float32Array {
  const ratio = inputSampleRate / TARGET_SAMPLE_RATE;
  const outputLength = Math.floor(input.length / ratio);
  const output = new Float32Array(outputLength);

  let offsetResult = 0;
  let offsetBuffer = 0;

  while (offsetResult < outputLength) {
    const nextOffsetBuffer = Math.round((offsetResult + 1) * ratio);
    let accum = 0;
    let count = 0;

    for (let i = offsetBuffer; i < nextOffsetBuffer && i < input.length; i += 1) {
      accum += input[i];
      count += 1;
    }

    output[offsetResult] = count > 0 ? accum / count : 0;
    offsetResult += 1;
    offsetBuffer = nextOffsetBuffer;
  }

  return output;
}

export function floatTo16BitPCM(input: Float32Array): ArrayBuffer {
  const buffer = new ArrayBuffer(input.length * 2);
  const view = new DataView(buffer);
  let offset = 0;
  for (let i = 0; i < input.length; i += 1, offset += 2) {
    let s = Math.max(-1, Math.min(1, input[i]));
    s = s < 0 ? s * 0x8000 : s * 0x7fff;
    view.setInt16(offset, s, true);
  }
  return buffer;
}

export function mixDownToMono(channels: Float32Array[]): Float32Array {
  if (channels.length === 1) {
    return channels[0];
  }

  const length = channels[0].length;
  const output = new Float32Array(length);

  for (let i = 0; i < length; i += 1) {
    let sum = 0;
    for (let channelIndex = 0; channelIndex < channels.length; channelIndex += 1) {
      sum += channels[channelIndex][i];
    }
    output[i] = sum / channels.length;
  }

  return output;
}

export interface FrameAccumulator {
  append(samples: Float32Array): EncodedFrame[];
  reset(): void;
}

export function createFrameAccumulator(frameSize = 320): FrameAccumulator {
  let buffer = new Float32Array(0);

  return {
    append(samples: Float32Array) {
      if (buffer.length === 0) {
        buffer = samples;
      } else {
        const merged = new Float32Array(buffer.length + samples.length);
        merged.set(buffer);
        merged.set(samples, buffer.length);
        buffer = merged;
      }

      const frames: EncodedFrame[] = [];
      const step = frameSize;
      while (buffer.length >= step) {
        const chunk = buffer.slice(0, step);
        frames.push({ payload: floatTo16BitPCM(chunk), samples: chunk.length });
        buffer = buffer.slice(step);
      }

      return frames;
    },
    reset() {
      buffer = new Float32Array(0);
    }
  } satisfies FrameAccumulator;
}

export function getTimestamp(): number {
  return performance.now();
}

export const AUDIO_CONSTANTS = {
  targetSampleRate: TARGET_SAMPLE_RATE,
  frameSamples: 320,
  frameDurationMs: 20
};
