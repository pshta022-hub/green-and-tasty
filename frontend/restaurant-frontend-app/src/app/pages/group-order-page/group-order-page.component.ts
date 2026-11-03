import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { ToastService } from '@app/services/toast/toast.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

@Component({
  selector: 'app-group-order-page',
  imports: [
    ParticipantCardComponent,
    CurrencyPipe,
    DatePipe,
  ],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly route = inject(ActivatedRoute);
  private readonly toastService = inject(ToastService);

  readonly sessionData = signal<any>(null);
  readonly invite = signal<any>(null);
  readonly participants = signal<any[]>([]);
  readonly ownerSessionId = signal<string | null>(null);
  readonly status = signal<any>(null);

  readonly errorMessage = signal<string | null>(null);
  readonly errorCode = signal<number | null>(null);
  readonly isSubmitting = signal<boolean>(false);
  readonly isSubmittedSuccess = signal<boolean>(false);
  readonly showModal = signal<boolean>(false);

  readonly ownerParticipant = computed(() => {
    const ownerId = this.ownerSessionId();
    return this.participants().find((p) => p.id === ownerId);
  });

  readonly otherParticipants = computed(() => {
    const ownerId = this.ownerSessionId();
    return this.participants().filter((p) => p.id !== ownerId);
  });

  readonly unsubmittedCount = computed(() => {
    return this.participants().filter((p) => !p.isSubmitted).length;
  });

  readonly isEmptyOrder = computed(() => {
    const parts = this.participants();
    if (parts.length === 0) return true;
    return parts.every((p) => !p.items || p.items.length === 0);
  });

  readonly hasSubmittedParticipants = computed(() => {
    return this.participants().some((p) => p.isSubmitted);
  });

  readonly consolidatedItems = computed(() => {
  return consolidatePreOrder(this.participants());
});

  readonly grandTotal = computed(() => {
    return this.participants()
      .filter((p) => p.isSubmitted)
      .reduce((sum, p) => sum + (p.total || 0), 0);
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.errorMessage.set(null);
    this.errorCode.set(null);

    const reservationId = this.route.snapshot.paramMap.get('reservationId') || 'res-1';

    this.groupOrdersService.getGroupInvite$(reservationId).subscribe({
      next: (inviteRes: any) => {
        this.invite.set(inviteRes);
        const groupReservationId = inviteRes.GroupReservationId;

        this.groupOrdersService.getSessions$(groupReservationId).subscribe({
          next: (sessionsRes: any) => {
            this.sessionData.set(sessionsRes);
            if (sessionsRes?.participants) {
              this.participants.set(sessionsRes.participants);
            }
          },
          error: (err: HttpErrorResponse) => {
            console.error('Error loading sessions', err);
          },
        });

        this.groupOrdersService.getStatus$(groupReservationId).subscribe({
          next: (statusRes: any) => this.status.set(statusRes),
          error: (err) => console.error('Error loading status', err),
        });
      },
      error: (err: HttpErrorResponse) => {
        this.errorCode.set(err.status);
        if (err.status === 404) {
          this.errorMessage.set('Group reservation not found');
        } else {
          this.errorMessage.set('Error loading reservation');
        }
      },
    });

    this.groupOrdersService.getOwnerSessionId$(reservationId).subscribe({
      next: (ownerId: string) => this.ownerSessionId.set(ownerId),
      error: (err) => console.error('Error loading owner session id', err),
    });
  }

  // Головний метод, який викликається при кліку на кнопку Submit Pre-Order
  submitPreOrder(): void {
    if (!this.hasSubmittedParticipants() || this.isSubmitting()) return;
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  confirmSubmit(): void {
    if (this.isSubmitting()) return;
    this.isSubmitting.set(true);

    const groupReservationId = this.invite()?.GroupReservationId;
    const reservationId = this.route.snapshot.paramMap.get('reservationId') || 'res-1';

    this.groupOrdersService.submit$(groupReservationId, reservationId).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.isSubmittedSuccess.set(true);
        this.showModal.set(false);
        this.toastService.success('Pre-order submitted successfully');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.showModal.set(false);
        console.error('Error submitting pre-order', err);
      },
    });
  }
}