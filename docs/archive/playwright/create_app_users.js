// Playwright automation script (best-effort) to log into Casdoor and create users.
// Notes:
// - This script uses heuristic selectors because the target UI may vary. Inspect and
//   adjust selectors in case of failures.
// - It expects environment variables: CASDOOR_URL, ADMIN_USER, ADMIN_PASS
// - Users to create are read from users-to-create.json in the same directory.
// - The script saves screenshots and a Playwright trace (trace.zip) to the current folder.

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

function tryGetEnv(name, fallback) {
  return process.env[name] || fallback;
}

const BASE_URL = tryGetEnv('CASDOOR_URL', 'https://sso.fzcsps.com');
const ADMIN_USER = tryGetEnv('ADMIN_USER', 'admin');
const ADMIN_PASS = tryGetEnv('ADMIN_PASS', '123');

const USERS_FILE = path.join(__dirname, 'users-to-create.json');

async function loadUsers() {
  if (!fs.existsSync(USERS_FILE)) {
    console.log(`users-to-create.json not found; creating sample at ${USERS_FILE}`);
    const sample = [
      {
        "owner": "app",
        "name": "app-built-in",
        "displayName": "app-built-in",
        "password": "P@ssw0rd!",
        "email": "app-built-in@example.local"
      }
    ];
    fs.writeFileSync(USERS_FILE, JSON.stringify(sample, null, 2), { encoding: 'utf8' });
  }
  const txt = fs.readFileSync(USERS_FILE, { encoding: 'utf8' });
  return JSON.parse(txt);
}

async function fillFirst(page, selectors, value) {
  for (const s of selectors) {
    try {
      const el = await page.$(s);
      if (el) {
        await el.fill(value);
        return true;
      }
    } catch (e) {}
  }
  return false;
}

async function clickFirst(page, selectors) {
  for (const s of selectors) {
    try {
      const el = await page.$(s);
      if (el) {
        await el.click({ timeout: 5000 });
        return true;
      }
    } catch (e) {}
  }
  return false;
}

async function attemptLogin(page) {
  console.log('打开登录页：', BASE_URL + '/login');
  await page.goto(BASE_URL + '/login', { waitUntil: 'networkidle' });

  // 尝试若干常见输入选择器
  const userSelectors = ['input[name="username"]', 'input[name="user"]', 'input[name="login"]', 'input[name="email"]', 'input[type="text"]', 'input[type="email"]'];
  const passSelectors = ['input[name="password"]', 'input[type="password"]'];
  const submitSelectors = ['button[type="submit"]', 'button:has-text("Sign in")', 'button:has-text("登录")', 'button:has-text("Sign In")'];

  const gotUser = await fillFirst(page, userSelectors, ADMIN_USER);
  const gotPass = await fillFirst(page, passSelectors, ADMIN_PASS);
  if (!gotUser) console.warn('未找到用户名输入框，稍后会截图并退出。');
  if (!gotPass) console.warn('未找到密码输入框，稍后会截图并退出。');

  const clicked = await clickFirst(page, submitSelectors);
  if (!clicked) {
    console.warn('未找到可点击的提交按钮，稍后会截图并退出。');
  } else {
    try {
      // 等待导航或页面变化
      await page.waitForTimeout(2500);
      await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(()=>{});
    } catch (e) {}
  }

  // 检查是否登录成功：页面 URL 发生变化或包含登出/退出按钮
  const url = page.url();
  const body = await page.content();
  const loggedIn = url.indexOf('/login') < 0 && (body.indexOf('logout') >= 0 || body.indexOf('退出') >= 0 || body.indexOf('Sign out') >= 0);
  if (!loggedIn) {
    console.error('登录似乎未成功；已保存截图以供检查。');
    await page.screenshot({ path: 'playwright-login-failed.png', fullPage: true });
    return false;
  }
  console.log('登录成功，当前页面：', url);
  await page.screenshot({ path: 'playwright-after-login.png', fullPage: true });
  return true;
}

async function navigateToUsers(page) {
  // 尝试通过页面文本导航至 Users 页面
  const userMenuSelectors = ['text=Users', 'text=用户', 'text=Users & Groups', 'text=People', 'a:has-text("Users")', 'a:has-text("用户")'];
  for (const sel of userMenuSelectors) {
    try {
      const loc = page.locator(sel).first();
      if (await loc.count() > 0) {
        await loc.click({ timeout: 5000 });
        await page.waitForLoadState('networkidle');
        console.log('已通过文本导航到用户页，选择器：', sel);
        return true;
      }
    } catch (e) {}
  }
  // 作为后备，尝试常见管理子路径
  const tryPaths = ['/admin', '/admin/users', '/users', '/organization/users', '/admin/user'];
  for (const p of tryPaths) {
    try {
      await page.goto(BASE_URL + p, { waitUntil: 'networkidle' });
      const body = await page.content();
      if (body.toLowerCase().indexOf('user') >= 0 || body.indexOf('用户') >= 0) {
        console.log('已访问候选用户页面：', p);
        await page.screenshot({ path: `playwright-navigate-${p.replace(/\W+/g, '_')}.png`, fullPage: true });
        return true;
      }
    } catch (e) {}
  }
  console.warn('未能自动定位到用户管理页，已截图以供人工确认。');
  await page.screenshot({ path: 'playwright-navigate-users-failed.png', fullPage: true });
  return false;
}

async function createUserViaUI(page, u) {
  console.log('准备创建用户：', u.owner + '/' + u.name);
  // 尝试点击“Add User”或“New User”按钮
  const addSelectors = ['text=Add User', 'text=新增用户', 'text=New User', 'button:has-text("Add")', 'button:has-text("Add user")'];
  const added = await clickFirst(page, addSelectors);
  if (!added) {
    console.warn('未找到新增用户按钮，尝试通过页面路径创建 (可能需要在 UI 手动完成)。');
    return false;
  }

  await page.waitForTimeout(800);

  // 常见字段填充候选
  await fillFirst(page, ['input[name="owner"]', 'input#owner', 'input[placeholder="Owner"]'], u.owner || 'app');
  await fillFirst(page, ['input[name="name"]', 'input#name', 'input[placeholder="Name"]'], u.name || 'app-built-in');
  await fillFirst(page, ['input[name="displayName"]', 'input#displayName', 'input[placeholder="Display Name"]'], u.displayName || u.name);
  await fillFirst(page, ['input[name="email"]', 'input#email', 'input[placeholder="Email"]'], u.email || 'no-reply@example.local');
  await fillFirst(page, ['input[name="password"]', 'input#password', 'input[placeholder="Password"]'], u.password || 'P@ssw0rd!');

  // 尝试提交
  const submitSelectors = ['button:has-text("Create")', 'button:has-text("保存")', 'button:has-text("Submit")', 'button[type="submit"]'];
  const sub = await clickFirst(page, submitSelectors);
  if (!sub) {
    console.warn('未能找到提交按钮，已截图。');
    await page.screenshot({ path: `playwright-create-${u.name}-no-submit.png`, fullPage: true });
    return false;
  }
  await page.waitForTimeout(1200);
  await page.screenshot({ path: `playwright-created-${u.name}.png`, fullPage: true });
  console.log('完成创建用户（或尝试已完成）：', u.owner + '/' + u.name);
  return true;
}

(async () => {
  const users = await loadUsers();

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  await context.tracing.start({ screenshots: true, snapshots: true });
  const page = await context.newPage();

  try {
    const ok = await attemptLogin(page);
    if (!ok) {
      console.error('登录未成功，脚本退出。请打开 playwright-playground 截图检查 DOM 并调整选择器后重试。');
      await context.tracing.stop({ path: 'trace-login-failed.zip' });
      await browser.close();
      process.exit(2);
    }

    const navOk = await navigateToUsers(page);
    if (!navOk) {
      console.warn('无法自动定位到用户管理页；请手动确认页面并继续。');
    }

    for (const u of users) {
      try {
        const created = await createUserViaUI(page, u);
        if (!created) {
          console.warn('创建失败，保存页面以供分析。');
        }
      } catch (e) {
        console.error('创建用户时抛出异常：', e && e.message);
      }
    }

    // 结束 tracing 并保存
    await context.tracing.stop({ path: 'trace.zip' });
    console.log('已完成所有尝试。tracing 保存为 trace.zip；截图保存在工作目录。');
  } finally {
    await browser.close();
  }
})();
