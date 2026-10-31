const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require('playwright');

const root = __dirname;
const screenshots = path.join(root, 'screenshots');
const states = ['resident', 'build', 'research', 'dynasty', 'events', 'army', 'map', 'diplomacy'];
const sizes = [
  { width: 1920, height: 1080, tag: 'desktop' },
  { width: 1280, height: 720, tag: 'minimum' }
];

function ensure(condition, message) {
  if (!condition) throw new Error(message);
}

(async () => {
  fs.mkdirSync(screenshots, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const report = [];

  try {
    for (const size of sizes) {
      const page = await browser.newPage({ viewport: size, deviceScaleFactor: 1 });
      const errors = [];
      page.on('console', message => {
        if (message.type() === 'error') errors.push(`console: ${message.text()}`);
      });
      page.on('pageerror', error => errors.push(`page: ${error.message}`));
      await page.goto(pathToFileURL(path.join(root, 'index.html')).href);

      for (const state of states) {
        await page.evaluate(name => show(name), state);
        await page.screenshot({ path: path.join(screenshots, `${size.tag}-${state}.png`) });
      }

      for (const state of ['settlement', 'resident', 'build', 'research', 'events', 'map', 'about']) {
        await page.locator(`[data-go=${state}]`).first().click();
        ensure(await page.locator('.prototype').getAttribute('data-screen') === state,
          `${size.tag}: navigation to ${state} failed`);
      }

      await page.keyboard.press('9');
      ensure(await page.locator('.prototype').getAttribute('data-screen') === 'map',
        `${size.tag}: keyboard navigation failed`);
      await page.locator('.workspace-head .action').click();
      ensure(await page.locator('.prototype').getAttribute('data-screen') === 'diplomacy',
        `${size.tag}: map to diplomacy navigation failed`);

      const layout = await page.evaluate(() => ({
        viewportWidth: innerWidth,
        bodyWidth: document.body.scrollWidth,
        prototypeWidth: document.querySelector('.prototype').scrollWidth,
        visibleButtons: [...document.querySelectorAll('button')].filter(x => x.offsetParent).length
      }));
      ensure(layout.bodyWidth === layout.viewportWidth && layout.prototypeWidth === layout.viewportWidth,
        `${size.tag}: horizontal overflow detected`);
      ensure(errors.length === 0, `${size.tag}: ${errors.join('; ')}`);
      report.push({ size, statesRendered: states.length, layout, errors });
      await page.close();
    }
  } finally {
    await browser.close();
  }

  fs.writeFileSync(path.join(root, 'verification-report.json'), JSON.stringify({ passed: true, report }, null, 2));
  console.log(`UI01 prototype verification passed: ${sizes.length} viewports, ${states.length * sizes.length} screenshots.`);
})().catch(error => {
  console.error(error);
  process.exit(1);
});
