import { Injectable, signal } from '@angular/core';

/** How the active schools are averaged on a node: one vote per school, or one per family of traditions. */
export type ConsensusMode = 'plain' | 'lineage';

const KEY = 'dendera.consensus';

/** Per-viewer choice (browser storage); pages pass it to the API (`?consensus=lineage`). */
@Injectable({ providedIn: 'root' })
export class ConsensusStore {
  readonly mode = signal<ConsensusMode>(read());

  set(mode: ConsensusMode): void {
    this.mode.set(mode);
    try {
      localStorage.setItem(KEY, mode);
    } catch {
      /* storage unavailable: fine */
    }
  }
}

function read(): ConsensusMode {
  try {
    return localStorage.getItem(KEY) === 'lineage' ? 'lineage' : 'plain';
  } catch {
    return 'plain';
  }
}
