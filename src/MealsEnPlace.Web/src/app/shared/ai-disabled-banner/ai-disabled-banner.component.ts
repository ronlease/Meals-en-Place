import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AiAvailabilityService } from '../../core/services/ai-availability.service';

/**
 * Persistent banner rendered above the app content when no Claude API key is
 * configured. Links to <c>/settings</c> so the user can paste a key. The
 * dismiss button hides it for the rest of the session; a fresh app load
 * brings it back until a key is saved.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink],
  selector: 'app-ai-disabled-banner',
  styles: [
    `
      .banner {
        align-items: center;
        background: rgba(230, 150, 60, 0.14);
        border-bottom: 1px solid rgba(230, 150, 60, 0.35);
        color: #8a5a14;
        display: flex;
        gap: 8px;
        padding: 8px 16px;
      }

      .banner mat-icon {
        flex-shrink: 0;
      }

      .banner-message {
        flex: 1;
      }

      .banner a {
        color: inherit;
        font-weight: 600;
        text-decoration: underline;
      }

      .banner button {
        color: inherit;
      }
    `,
  ],
  template: `
    @if (!ai.configured() && !ai.dismissed()) {
      <div class="banner" role="alert" aria-live="polite">
        <mat-icon fontIcon="warning" inline></mat-icon>
        <span class="banner-message">
          AI features are disabled.
          <a routerLink="/settings">Add a Claude API key</a>
          to enable dietary classification, recipe substitution suggestions, and
          meal plan optimization.
        </span>
        <button
          mat-icon-button
          aria-label="Dismiss banner for this session"
          (click)="ai.dismissBanner()"
        >
          <mat-icon fontIcon="close" inline></mat-icon>
        </button>
      </div>
    }
  `,
})
export class AiDisabledBannerComponent {
  protected readonly ai = inject(AiAvailabilityService);
}
