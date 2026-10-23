/**
 * The two page loads BEH-USERS-1's connect flow makes, behind one object so a
 * test can watch them (jsdom cannot navigate).
 */
export const githubNavigation = {
  to: (url: string) => window.location.assign(url),
  reload: () => window.location.replace(window.location.pathname),
}
