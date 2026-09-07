import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioChange, MatRadioModule } from '@angular/material/radio';
import {
  TodoistProjectEntry,
  TodoistProjectHistoryResponse,
  TodoistPushResourceType,
} from '../../core/models/todoist.models';
import { SettingsService } from '../../core/services/settings.service';

/** Data injected into the dialog by the caller. */
export interface TodoistProjectPickerDialogData {
  /**
   * Which push surface opened the dialog. The last-used project is tracked
   * per resource type by the API (derived from the ExternalTaskLink push
   * history), so the caller supplies only the surface — not a stored ID.
   */
  resourceType: TodoistPushResourceType;
}

/**
 * Value returned by the dialog when the user confirms.
 * - `null`: push to Inbox (default).
 * - A non-empty string: push to the specified Todoist project.
 * The dialog closes with `undefined` when dismissed without confirming.
 */
export interface TodoistProjectPickerResult {
  projectId: string | null;
}

/** Fallback Inbox entry used when the history call itself fails. */
const INBOX_ENTRY: TodoistProjectEntry = {
  displayName: 'Inbox (default)',
  isInbox: true,
  projectId: null,
};

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatRadioModule,
  ],
  selector: 'app-todoist-project-picker-dialog',
  standalone: true,
  styles: [
    `
      .loading-row {
        align-items: center;
        display: flex;
        gap: 12px;
        min-width: 280px;
        padding: 16px 0;
      }

      .names-hint {
        align-items: center;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.54));
        display: flex;
        font-size: 12px;
        gap: 6px;
        margin-bottom: 12px;

        mat-icon {
          font-size: 16px;
          height: 16px;
          width: 16px;
        }
      }

      .project-list {
        display: flex;
        flex-direction: column;
        gap: 4px;
        min-width: 280px;
      }

      .project-option {
        display: block;
      }
    `,
  ],
  template: `
    <h2 mat-dialog-title>Select Todoist project</h2>
    <mat-dialog-content>
      @if (loading()) {
        <div class="loading-row">
          <mat-progress-spinner mode="indeterminate" diameter="28" />
          <span>Loading projects…</span>
        </div>
      } @else {
        @if (!namesResolved()) {
          <div class="names-hint">
            <mat-icon>info</mat-icon>
            Project names could not be loaded — showing project IDs.
          </div>
        }
        <mat-radio-group
          class="project-list"
          [value]="selectedProjectId()"
          (change)="selectProject($event)"
        >
          @for (project of projects(); track project.projectId) {
            <mat-radio-button class="project-option" [value]="project.projectId">
              {{ project.isInbox ? 'Inbox (default)' : (project.displayName ?? project.projectId) }}
            </mat-radio-button>
          }
        </mat-radio-group>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(undefined)">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="loading()" (click)="confirm()">
        Push
      </button>
    </mat-dialog-actions>
  `,
})
export class TodoistProjectPickerDialogComponent implements OnInit {
  protected readonly data = inject<TodoistProjectPickerDialogData>(MAT_DIALOG_DATA);
  protected readonly dialogRef = inject(
    MatDialogRef<TodoistProjectPickerDialogComponent, TodoistProjectPickerResult | undefined>,
  );
  protected readonly loading = signal(true);
  protected readonly namesResolved = signal(true);
  protected readonly projects = signal<TodoistProjectEntry[]>([]);
  protected readonly selectedProjectId = signal<string | null>(null);

  private readonly settingsService = inject(SettingsService);

  private applyPreselection(response: TodoistProjectHistoryResponse): void {
    const lastUsed =
      this.data.resourceType === 'mealPlan'
        ? response.lastUsedMealPlanProjectId
        : response.lastUsedShoppingListProjectId;

    // A project the user has since deleted in Todoist no longer appears in the
    // list; fall back to Inbox rather than pre-selecting a dead ID.
    const exists = lastUsed !== null && response.projects.some((p) => p.projectId === lastUsed);
    this.selectedProjectId.set(exists ? lastUsed : null);
  }

  confirm(): void {
    this.dialogRef.close({ projectId: this.selectedProjectId() });
  }

  ngOnInit(): void {
    this.settingsService.getProjectHistory().subscribe({
      error: () => {
        this.loading.set(false);
        this.namesResolved.set(false);
        this.projects.set([INBOX_ENTRY]);
        this.selectedProjectId.set(null);
      },
      next: (response) => {
        this.loading.set(false);
        this.namesResolved.set(response.namesResolved);
        this.projects.set(response.projects);
        this.applyPreselection(response);
      },
    });
  }

  selectProject(event: MatRadioChange): void {
    this.selectedProjectId.set(event.value as string | null);
  }
}
