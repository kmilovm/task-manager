import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.auth.currentUser;
  readonly isAuthenticated = this.auth.isAuthenticated;

  ngOnInit(): void {
    if (!this.isAuthenticated()) {
      return;
    }

    // Refreshes the stored session against the server. A rejected token is handled by the auth
    // interceptor, which signs the account out, so there is nothing to do on failure here.
    this.auth.loadProfile().subscribe({ error: () => undefined });
  }

  signOut(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
