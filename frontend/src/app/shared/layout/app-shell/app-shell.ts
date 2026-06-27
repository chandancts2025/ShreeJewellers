import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Footer } from '../../components/footer/footer';
import { ToastHost } from '../../components/toast-host/toast-host';
import { TopNav } from '../../components/top-nav/top-nav';

@Component({
  selector: 'app-app-shell',
  imports: [RouterOutlet, TopNav, Footer, ToastHost],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss'
})
export class AppShell {
}
