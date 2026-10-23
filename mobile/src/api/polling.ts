import { pending } from './types';
export function jobPoller<T extends { state: string }>(
  load: (signal: AbortSignal) => Promise<T>,
  value: (job: T) => void,
  error: (e: unknown) => void,
  schedule: (run: () => void, delay: number) => () => void = (run, delay) => {
    const timer = setTimeout(run, delay);
    return () => clearTimeout(timer);
  },
) {
  let generation = 0;
  let cancelTimer = () => {};
  let controller: AbortController | undefined;
  function stop() {
    generation++;
    cancelTimer();
    controller?.abort();
  }
  function start() {
    stop();
    const current = generation;
    let failures = 0;
    async function poll() {
      if (current !== generation) return;
      const request = new AbortController();
      controller = request;
      try {
        const job = await load(request.signal);
        if (current !== generation) return;
        failures = 0;
        value(job);
        if (pending(job.state)) cancelTimer = schedule(() => void poll(), 2500);
      } catch (e) {
        if (current !== generation || request.signal.aborted) return;
        error(e);
        failures++;
        if (failures < 5)
          cancelTimer = schedule(() => void poll(), Math.min(30000, 3000 * 2 ** failures));
      }
    }
    void poll();
  }
  return { start, stop };
}
