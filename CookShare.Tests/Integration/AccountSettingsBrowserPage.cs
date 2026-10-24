namespace CookShare.Tests.Integration
{
    internal static class AccountSettingsBrowserPage
    {
        internal const string Html = """
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8"><title>Live Account Settings verification</title></head>
            <body style="margin:16px;font-family:system-ui;background:#f4f8f4">
              <h1>CookShare Account Settings — live browser verification</h1>
              <p>Actual Razor forms, Identity cookies, isolated SQLite and a recording email sender.</p>
              <pre id="results">RUNNING</pre>
              <iframe id="application" title="CookShare under test" style="width:1440px;height:900px;border:1px solid #245c43"></iframe>
              <script src="/__browser/runner.js"></script>
            </body></html>
            """;

        internal const string JavaScript = """
            (async () => {
              const iframe = document.getElementById('application');
              const checks = [];
              const result = document.getElementById('results');
              const name = 'Bếp trình duyệt';
              const oldEmail = 'browser-cook@example.com';
              const newEmail = 'browser-updated@example.com';
              const initialPassword = 'BrowserPassword123!';
              const changedPassword = 'ChangedPassword123!';
              const resetPassword = 'ResetPassword123!';
              const doc = () => iframe.contentDocument;
              const text = () => doc().body.innerText;
              const path = () => iframe.contentWindow.location.pathname;
              const assert = (condition, message) => { if (!condition) throw new Error(message); };
              const pass = message => { checks.push('PASS: ' + message); result.textContent = checks.join('\n'); };
              const state = () => fetch('/__browser/state').then(response => response.json());
              async function mail(email) {
                const response = await fetch('/__browser/email?email=' + encodeURIComponent(email));
                const data = await response.json();
                assert(data.path, 'A confirmation/reset email must be recorded');
                return data.path;
              }
              function loaded(action) {
                return new Promise((resolve, reject) => {
                  const timer = setTimeout(() => reject(new Error('Application navigation timed out')), 12000);
                  iframe.onload = () => { clearTimeout(timer); resolve(); };
                  action();
                });
              }
              const go = url => loaded(() => { iframe.src = url; });
              async function submit(selector, values = {}) {
                const form = doc().querySelector(selector);
                assert(form, 'Required form exists: ' + selector);
                for (const [field, value] of Object.entries(values)) {
                  const input = Array.from(form.elements).find(item => item.name === field && item.type !== 'hidden');
                  assert(input, 'Required field exists: ' + field);
                  if (input.type === 'checkbox') input.checked = value;
                  else input.value = value;
                  input.dispatchEvent(new iframe.contentWindow.Event('input', { bubbles: true }));
                  input.dispatchEvent(new iframe.contentWindow.Event('change', { bubbles: true }));
                }
                await loaded(() => form.requestSubmit());
              }
              async function login(password) {
                await go('/Account/Login');
                await submit('form.auth-form', { UserNameOrEmail: 'browser-cook', Password: password });
              }
              async function logout() {
                await submit('form[action="/Account/Logout"]');
              }
              try {
                await go('/Account/Settings');
                assert(path() === '/Account/Login', 'Settings requires authentication');
                pass('Anonymous Settings access redirects to login');
                await login(initialPassword);
                await go('/Account/Settings');
                assert(text().includes(oldEmail) && text().includes('Email chưa xác minh'), 'Current email and verification state are rendered');
                assert(doc().querySelector('a[href="/Profile/Edit"]'), 'Settings links to existing Profile Edit');
                pass('Real Identity cookie login opens Settings and Profile Edit navigation');

                for (const width of [360, 768, 1440]) {
                  iframe.style.width = width + 'px';
                  await new Promise(resolve => setTimeout(resolve, 50));
                  const viewport = iframe.contentWindow.innerWidth;
                  assert(viewport === width, 'Viewport has the requested width');
                  assert(doc().documentElement.scrollWidth <= viewport + 1, 'No horizontal overflow at ' + width + 'px');
                  for (const control of doc().querySelectorAll('.settings-card input:not([type="hidden"]), .settings-card button')) {
                    const rectangle = control.getBoundingClientRect();
                    assert(rectangle.width > 0 && rectangle.left >= -1 && rectangle.right <= viewport + 1,
                      'Settings controls fit the viewport at ' + width + 'px');
                  }
                  pass('Settings layout and controls fit ' + width + 'px');
                }

                await go('/Profile/Edit');
                await submit('.creator-edit-form', { DisplayName: name, AvatarUrl: '/img/recipes/profile-avatar.webp' });
                assert(path() === '/Profile' && text().includes(name), 'Profile update is visible');
                await go('/Account/Settings');
                assert(text().includes(name), 'Settings reads the updated DisplayName');
                pass('Existing Profile Edit updates DisplayName/avatar and Settings reflects it');

                await submit('form[action="/Account/ChangePassword"]', {
                  'PasswordForm.CurrentPassword': 'IncorrectPassword123!',
                  'PasswordForm.NewPassword': changedPassword,
                  'PasswordForm.ConfirmPassword': changedPassword
                });
                assert(text().includes('Mật khẩu hiện tại không chính xác'), 'Wrong current password is rejected');
                pass('Change Password displays Vietnamese rejection for a wrong current password');
                await submit('form[action="/Account/ChangePassword"]', {
                  'PasswordForm.CurrentPassword': initialPassword,
                  'PasswordForm.NewPassword': changedPassword,
                  'PasswordForm.ConfirmPassword': changedPassword
                });
                assert(text().includes('Đã đổi mật khẩu'), 'Password change succeeds');
                assert((await state()).displayName === name, 'Password change preserves profile');
                pass('Change Password succeeds with the correct password and preserves Profile');

                await submit('form[action="/Account/SendEmailConfirmation"]');
                await go(await mail(oldEmail));
                assert(text().includes('đã được xác minh') && (await state()).emailConfirmed, 'Email confirmation succeeds');
                pass('Recorded email confirmation link verifies the registered address');
                await go('/Account/ConfirmEmail?userId=browser-owner&code=invalid');
                assert(text().includes('Liên kết không hợp lệ hoặc đã hết hạn'), 'Invalid confirmation is rejected');
                pass('An invalid email confirmation token is rejected in the browser');

                await go('/Account/Settings');
                await submit('form[action="/Account/ChangeEmail"]', {
                  'EmailForm.NewEmail': newEmail, 'EmailForm.CurrentPassword': changedPassword
                });
                assert((await state()).email === oldEmail, 'Email must wait for confirmation');
                pass('Change Email leaves the old address active while confirmation is pending');
                await go(await mail(newEmail));
                const changed = await state();
                assert(changed.email === newEmail && changed.emailConfirmed && changed.displayName === name,
                  'The new address is confirmed and profile is preserved');
                pass('The link sent to the new address confirms Change Email and preserves Profile');

                await logout();
                await go('/Account/ForgotPassword');
                await submit('form.auth-form', { Email: newEmail });
                const knownNotice = doc().querySelector('.auth-subtitle').innerText;
                const resetLink = await mail(newEmail);
                await go('/Account/ForgotPassword');
                await submit('form.auth-form', { Email: 'unknown-browser@example.com' });
                assert(doc().querySelector('.auth-subtitle').innerText === knownNotice, 'Forgot responses must match');
                pass('Forgot Password shows the same notice for known and unknown addresses');
                await go(resetLink);
                await submit('form.auth-form', { Password: resetPassword, ConfirmPassword: resetPassword });
                assert(text().includes('Đã đặt lại mật khẩu'), 'Password reset succeeds');
                pass('The recorded reset link opens the real form and resets the password');
                await login(changedPassword);
                assert(path() === '/Account/Login' && text().includes('không chính xác'), 'Previous password is rejected');
                await login(resetPassword);
                await go('/Account/Settings');
                assert(path() === '/Account/Settings' && text().includes(newEmail), 'New reset password logs in');
                pass('The old password fails and the reset password signs in with a real cookie');

                await submit('form[action="/Account/DeleteAccount"]', { 'DeleteForm.Password': 'IncorrectPassword123!', 'DeleteForm.Confirm': true });
                assert(text().includes('Mật khẩu không chính xác') && (await state()).exists, 'Wrong deletion password is rejected');
                pass('Delete Account rejects a wrong password without deleting the account');
                await submit('form[action="/Account/DeleteAccount"]', { 'DeleteForm.Password': resetPassword, 'DeleteForm.Confirm': true });
                const deleted = await state();
                assert(!deleted.exists && deleted.unrelatedExists && deleted.importedExists, 'Only the authenticated test account is deleted');
                await go('/Account/Settings');
                assert(path() === '/Account/Login', 'Deletion signs the real browser session out');
                pass('Deletion signs out and preserves the unrelated user and imported recipe');
                document.body.dataset.testStatus = 'passed';
                await fetch('/__browser/result', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ passed: true, checks }) });
              } catch (error) {
                checks.push('FAIL: ' + error.message);
                result.textContent = checks.join('\n');
                document.body.dataset.testStatus = 'failed';
                await fetch('/__browser/result', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ passed: false, checks }) });
              }
            })();
            """;
    }
}
