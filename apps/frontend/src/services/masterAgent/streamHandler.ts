/**
 * SignalR Stream Handler
 * Utility for handling SignalR streaming with async generators
 */

import type { MasterStreamResponse } from './types';

/**
 * Creates an async generator from a SignalR stream subscription
 * This is a reusable utility for converting SignalR streams to async iterables
 */
export function createStreamGenerator<T extends MasterStreamResponse>(
  stream: { subscribe: (observer: StreamObserver<T>) => StreamSubscription }
): AsyncGenerator<T> {
  const queue: T[] = [];
  let resolveNext: ((value: IteratorResult<T>) => void) | null = null;
  let error: Error | null = null;
  let completed = false;
  let subscription: StreamSubscription | null = null;

  const generator: AsyncGenerator<T> = {
    async next(): Promise<IteratorResult<T>> {
      if (error) {
        throw error;
      }

      if (queue.length > 0) {
        return { value: queue.shift()!, done: false };
      }

      if (completed) {
        return { value: undefined as unknown as T, done: true };
      }

      return new Promise<IteratorResult<T>>((resolve) => {
        resolveNext = resolve;
      });
    },

    async return(): Promise<IteratorResult<T>> {
      subscription?.dispose();
      completed = true;
      return { value: undefined as unknown as T, done: true };
    },

    async throw(e: Error): Promise<IteratorResult<T>> {
      subscription?.dispose();
      error = e;
      throw e;
    },

    [Symbol.asyncIterator]() {
      return this;
    },
  };

  // Start subscription
  subscription = stream.subscribe({
    next: (item: T) => {
      if (resolveNext) {
        resolveNext({ value: item, done: false });
        resolveNext = null;
      } else {
        queue.push(item);
      }
    },
    error: (err: Error) => {
      error = err;
      if (resolveNext) {
        resolveNext({ value: undefined as unknown as T, done: true });
        resolveNext = null;
      }
    },
    complete: () => {
      completed = true;
      if (resolveNext) {
        resolveNext({ value: undefined as unknown as T, done: true });
        resolveNext = null;
      }
    },
  });

  return generator;
}

interface StreamObserver<T> {
  next: (item: T) => void;
  error: (err: Error) => void;
  complete: () => void;
}

interface StreamSubscription {
  dispose: () => void;
}
