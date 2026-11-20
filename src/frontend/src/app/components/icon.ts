import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { iconName } from '../core/icons';

/** A Material Symbol for an id of the registry (core/icons.ts), drawn in currentColor. */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="material-symbols-rounded" aria-hidden="true" [style.font-size.px]="size()">{{ glyph() }}</span>`,
  styles: `
    :host { display: inline-flex; vertical-align: middle; flex: none; line-height: 1; }
    span { line-height: 1; user-select: none; }
  `,
})
export class Icon {
  readonly name = input.required<string>();
  readonly size = input(18);
  protected readonly glyph = computed(() => iconName(this.name()));
}
