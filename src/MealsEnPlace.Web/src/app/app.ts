import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AiAvailabilityService } from './core/services/ai-availability.service';
import { InstallPromptService } from './core/services/install-prompt.service';
import { PreferencesService } from './core/services/preferences.service';
import { ThemeService } from './core/services/theme.service';
import { TodoistAvailabilityService } from './core/services/todoist-availability.service';
import { AiDisabledBannerComponent } from './shared/ai-disabled-banner/ai-disabled-banner.component';
import { OfflineBannerComponent } from './shared/offline-banner/offline-banner';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    AiDisabledBannerComponent,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatSidenavModule,
    MatToolbarModule,
    MatTooltipModule,
    OfflineBannerComponent,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  protected readonly aiAvailability = inject(AiAvailabilityService);
  protected readonly installPromptService = inject(InstallPromptService);
  protected readonly isMobile = signal(false);
  protected readonly preferencesService = inject(PreferencesService);
  protected readonly themeService = inject(ThemeService);
  protected readonly todoistAvailability = inject(TodoistAvailabilityService);

  private readonly breakpointObserver = inject(BreakpointObserver);

  @ViewChild('sidenav') sidenav!: MatSidenav;

  ngOnInit(): void {
    this.preferencesService.loadPreferences();
    this.aiAvailability.refresh();
    this.todoistAvailability.refresh();

    this.breakpointObserver
      .observe(['(max-width: 768px)'])
      .subscribe((result) => {
        this.isMobile.set(result.matches);
      });
  }

  onNavClick(): void {
    if (this.isMobile()) {
      this.sidenav.close();
    }
  }
}
