import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { NetworkStatusService } from '../../core/services/network-status.service';

@Component({
  selector: 'app-offline-banner',
  standalone: true,
  imports: [MatIconModule],
  template: `
    @if (!networkStatus.isOnline()) {
      <div class="offline-banner">
        <mat-icon>cloud_off</mat-icon>
        <span>You are offline — showing cached data</span>
      </div>
    }
  `,
  styles: `
    .offline-banner {
      align-items: center;
      background: #f57f17;
      color: #fff;
      display: flex;
      font-size: 14px;
      gap: 8px;
      justify-content: center;
      padding: 6px 16px;
    }
  `,
})
export class OfflineBannerComponent {
  protected readonly networkStatus = inject(NetworkStatusService);
}
