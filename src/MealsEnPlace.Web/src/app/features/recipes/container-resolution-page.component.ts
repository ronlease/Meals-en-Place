import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { UnresolvedGroupResponse } from '../../core/models/recipe.models';
import { RecipeService } from '../../core/services/recipe.service';
import {
  BulkResolveDialogComponent,
  BulkResolveDialogData,
  BulkResolveDialogResult,
} from './bulk-resolve-dialog.component';

@Component({
  selector: 'app-container-resolution-page',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Container Resolution Queue</h1>
      <p class="page-subtitle">
        Unresolved container references grouped by canonical ingredient and
        source phrase. Declaring a net weight here applies to every recipe
        that uses the same phrase.
      </p>
    </div>

    @if (loading()) {
      <div class="spinner-container">
        <mat-progress-spinner mode="indeterminate" diameter="48" />
      </div>
    } @else if (error()) {
      <div class="state-message error-message">
        <mat-icon>error_outline</mat-icon>
        <span>Failed to load the queue. Please try again.</span>
        <button mat-button color="primary" (click)="load()">Retry</button>
      </div>
    } @else if (groups().length === 0) {
      <div class="state-message">
        <mat-icon>check_circle_outline</mat-icon>
        <span>No unresolved container references. You're all caught up.</span>
      </div>
    } @else {
      <div class="summary-bar">
        <span class="summary-badge">
          {{ groups().length }} group{{ groups().length === 1 ? '' : 's' }}
        </span>
        <span class="summary-badge">
          {{ totalOccurrences() }} ingredient{{ totalOccurrences() === 1 ? '' : 's' }} pending
        </span>
      </div>

      <mat-table [dataSource]="groups()" class="resolution-table">
        <ng-container matColumnDef="canonicalIngredientName">
          <mat-header-cell *matHeaderCellDef>Canonical</mat-header-cell>
          <mat-cell *matCellDef="let group">
            <strong>{{ group.canonicalIngredientName }}</strong>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="notes">
          <mat-header-cell *matHeaderCellDef>Source Phrase</mat-header-cell>
          <mat-cell *matCellDef="let group" class="notes-cell">
            <code>{{ group.notes }}</code>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="occurrenceCount">
          <mat-header-cell *matHeaderCellDef>Occurrences</mat-header-cell>
          <mat-cell *matCellDef="let group">
            <span class="occurrence-badge">{{ group.occurrenceCount }}</span>
          </mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef></mat-header-cell>
          <mat-cell *matCellDef="let group" class="actions-cell">
            <button mat-flat-button color="primary" (click)="openResolveDialog(group)">
              Resolve
            </button>
          </mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
        <mat-row *matRowDef="let row; columns: displayedColumns"></mat-row>
      </mat-table>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        padding: 1.5rem;
      }

      .page-header {
        margin-bottom: 1.5rem;
      }

      .page-title {
        margin: 0 0 0.25rem 0;
        font-size: 1.75rem;
      }

      .page-subtitle {
        margin: 0;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
        max-width: 60ch;
      }

      .summary-bar {
        display: flex;
        gap: 0.75rem;
        margin-bottom: 1rem;
      }

      .summary-badge {
        background: var(--mat-sys-secondary-container, #e0e0e0);
        color: var(--mat-sys-on-secondary-container, #000);
        padding: 0.25rem 0.75rem;
        border-radius: 999px;
        font-size: 0.875rem;
      }

      .resolution-table {
        width: 100%;
        background: var(--mat-sys-surface, #fff);
      }

      .notes-cell code {
        font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
        font-size: 0.875rem;
        padding: 0.125rem 0.5rem;
        background: var(--mat-sys-surface-container, #f5f5f5);
        border-radius: 4px;
      }

      .occurrence-badge {
        background: var(--mat-sys-tertiary-container, #ffd8b1);
        color: var(--mat-sys-on-tertiary-container, #000);
        padding: 0.125rem 0.625rem;
        border-radius: 999px;
        font-weight: 600;
      }

      .actions-cell {
        justify-content: flex-end;
      }

      .spinner-container {
        display: flex;
        justify-content: center;
        padding: 3rem 0;
      }

      .state-message {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
        padding: 3rem 1rem;
        color: var(--mat-sys-on-surface-variant, rgba(0, 0, 0, 0.6));
      }

      .state-message mat-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
      }

      .error-message mat-icon {
        color: var(--mat-sys-error, #b00020);
      }
    `,
  ],
})
export class ContainerResolutionPageComponent implements OnInit {
  protected readonly displayedColumns = [
    'canonicalIngredientName',
    'notes',
    'occurrenceCount',
    'actions',
  ];

  protected readonly error = signal(false);
  protected readonly groups = signal<UnresolvedGroupResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly totalOccurrences = computed(() =>
    this.groups().reduce((sum, g) => sum + g.occurrenceCount, 0)
  );

  private readonly dialog = inject(MatDialog);
  private readonly recipeService = inject(RecipeService);
  private readonly snackBar = inject(MatSnackBar);

  ngOnInit(): void {
    this.load();
  }

  openResolveDialog(group: UnresolvedGroupResponse): void {
    const data: BulkResolveDialogData = {
      canonicalIngredientId: group.canonicalIngredientId,
      canonicalIngredientName: group.canonicalIngredientName,
      notes: group.notes,
      occurrenceCount: group.occurrenceCount,
    };

    const dialogRef = this.dialog.open<
      BulkResolveDialogComponent,
      BulkResolveDialogData,
      BulkResolveDialogResult | undefined
    >(BulkResolveDialogComponent, {
      data,
      width: '480px',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.snackBar.open(
          `Resolved ${result.affectedCount} ingredient${result.affectedCount === 1 ? '' : 's'}.`,
          'Dismiss',
          { duration: 4000 }
        );
        this.load();
      }
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.recipeService.getUnresolvedGroups().subscribe({
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
      next: (groups) => {
        this.groups.set(groups);
        this.loading.set(false);
      },
    });
  }
}
