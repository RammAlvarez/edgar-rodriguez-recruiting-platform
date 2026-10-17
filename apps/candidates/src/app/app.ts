import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionWait } from '@platform/ui/status-pages';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SessionWait],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<ui-session-wait /><router-outlet />',
})
export class App {}
