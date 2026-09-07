import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabGroup } from '@angular/material/tabs';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { InventoryLocation } from '../../core/models/inventory.models';
import { InventoryService } from '../../core/services/inventory.service';
import { InventoryPageComponent } from './inventory-page.component';
import { InventoryTableComponent } from './inventory-table.component';

describe('InventoryPageComponent', () => {
  let component: InventoryPageComponent;
  let dialogMock: { open: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<InventoryPageComponent>;

  function activeLocation(): InventoryLocation {
    return (component as unknown as { activeLocation: () => InventoryLocation }).activeLocation();
  }

  function tabGroup(): MatTabGroup {
    return fixture.debugElement.query(By.directive(MatTabGroup)).componentInstance;
  }

  /**
   * Selects a tab and flushes the lazy matTabContent portal. The portal attaches
   * in a microtask during the tab body's centering pass, so the DOM is only
   * settled once the fixture is stable again.
   */
  async function selectTab(index: number): Promise<void> {
    tabGroup().selectedIndex = index;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function renderedTables(): InventoryTableComponent[] {
    return fixture.debugElement
      .queryAll(By.directive(InventoryTableComponent))
      .map((node) => node.componentInstance as InventoryTableComponent);
  }

  beforeEach(() => {
    dialogMock = { open: vi.fn().mockReturnValue({ afterClosed: () => of(null) }) };

    TestBed.configureTestingModule({
      imports: [InventoryPageComponent, NoopAnimationsModule],
      providers: [
        { provide: MatDialog, useValue: dialogMock },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
        {
          provide: InventoryService,
          useValue: { deleteItem: vi.fn(), getItems: vi.fn().mockReturnValue(of([])) },
        },
      ],
    });

    fixture = TestBed.createComponent(InventoryPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Tabs ────────────────────────────────────────────────────────────────────

  describe('location tabs', () => {
    it('renders a tab per storage location', () => {
      const labels = Array.from(
        fixture.nativeElement.querySelectorAll('.mat-mdc-tab .mdc-tab__text-label'),
      ).map((label) => (label as HTMLElement).textContent?.trim());

      expect(labels).toEqual(['Pantry', 'Fridge', 'Freezer']);
    });

    it('starts on Pantry', () => {
      expect(activeLocation()).toBe('Pantry');
    });

    it('tracks the Fridge tab', () => {
      component.onTabChange(1);

      expect(activeLocation()).toBe('Fridge');
    });

    it('tracks the Freezer tab', () => {
      component.onTabChange(2);

      expect(activeLocation()).toBe('Freezer');
    });

    it('lazily renders only the selected tab, so one table exists at a time', () => {
      // matTabContent defers each table until its tab is shown.
      expect(renderedTables().length).toBe(1);
      expect(renderedTables()[0].location()).toBe('Pantry');
    });

    it('renders the Freezer table once that tab is selected', async () => {
      await selectTab(2);

      expect(renderedTables().map((table) => table.location())).toEqual(['Freezer']);
    });
  });

  // ── Add Item delegates to the visible table ──────────────────────────────────

  describe('addItem', () => {
    it('opens the add dialog on the table for the active location', () => {
      const table = renderedTables()[0];
      const openAddDialog = vi.spyOn(table, 'openAddDialog');

      component.addItem();

      expect(openAddDialog).toHaveBeenCalled();
    });

    it('targets the Freezer table after switching to that tab', async () => {
      await selectTab(2);
      component.onTabChange(2);
      const table = renderedTables()[0];
      const openAddDialog = vi.spyOn(table, 'openAddDialog');

      component.addItem();

      expect(openAddDialog).toHaveBeenCalled();
      expect(table.location()).toBe('Freezer');
    });

    it('does nothing when no table matches the active location', () => {
      // The tab's content is lazy, so a location change without a render leaves
      // no matching table — the click must be a safe no-op rather than a crash.
      component.onTabChange(1);

      expect(() => component.addItem()).not.toThrow();
      expect(dialogMock.open).not.toHaveBeenCalled();
    });

    it('is wired to the Add Item button in the header', () => {
      const addItem = vi.spyOn(component, 'addItem');
      const button = fixture.nativeElement.querySelector(
        '.page-header button',
      ) as HTMLButtonElement;

      button.click();

      expect(addItem).toHaveBeenCalled();
    });
  });
});
