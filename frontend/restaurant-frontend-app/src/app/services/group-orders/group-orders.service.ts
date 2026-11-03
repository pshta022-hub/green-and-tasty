
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, switchMap, map } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private url(path: string): string {
    return `${this.apiBaseUrl.replace(/\/+$/, '')}${path}`;
  }

  getGroupInvite$(reservationId: string): Observable<any> {
    return this.http
      .get<{ token: string }>(this.url(`/reservations/${reservationId}/invite-token`))
      .pipe(
        switchMap((res) =>
          this.http.get<any>(this.url(`/guest/invites/${res.token}`))
        )
      );
  }

  getSessions$(groupReservationId: string): Observable<any> {
    return this.http.get<any>(
      this.url(`/guest/groups/${groupReservationId}/sessions`)
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<{ guestSessionId: string }>(
        this.url(`/reservations/${reservationId}/guest-session-id`)
      )
      .pipe(map((res) => res.guestSessionId));
  }

  getStatus$(groupReservationId: string): Observable<any> {
    return this.http.get<any>(
      this.url(`/guest/groups/${groupReservationId}/status`)
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<any> {
    return this.http.post(
      this.url(`/group-reservations/${groupReservationId}/submit`),
      { reservationId }
    );
  }
}
