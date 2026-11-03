
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();

  readonly title = computed(() => {
    const person = this.participant();

    return person.type === 'Owner'
      ? `Owner: ${person.name}`
      : `Guest: ${person.name}`;
  });
  readonly groupedItems = computed(() => {
  const grouped = new Map<string, { dish: any; quantity: number }>();

  for (const item of this.participant().items) {
    const existing = grouped.get(item.dish.id);

    grouped.set(item.dish.id, {
      dish: item.dish,
      quantity: (existing?.quantity ?? 0) + item.quantity,
    });
  }

  return [...grouped.values()];
});
}
