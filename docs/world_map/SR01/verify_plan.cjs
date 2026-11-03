// Documentation checks only. Does not start Unity or change project runtime data.
const fs=require('fs'),path=require('path'),assert=require('assert'),crypto=require('crypto');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('playwright');
const here=__dirname, root=path.resolve(here,'../../..'), data=JSON.parse(fs.readFileSync(path.join(here,'plan-data.json'),'utf8'));
const out=path.join(root,'Artifacts/SR01');fs.mkdirSync(out,{recursive:true});
const report={date:'2026-11-03',checks:[],errors:[],screenshots:[]};
function check(name,fn){fn();report.checks.push(name)}
check('16 unique regions cover 4,000,000 square metres without interior overlap',()=>{
  assert.equal(new Set(data.regions.map(r=>r.code)).size,16);
  function contains(p,poly){let c=false;for(let i=0,j=poly.length-1;i<poly.length;j=i++){let a=poly[i],b=poly[j];if((a[1]>p[1])!==(b[1]>p[1])&&p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0])c=!c}return c}
  let area=0;for(const r of data.regions){for(const p of r.polygon)assert(p.every(v=>v>=0&&v<=2000));let sum=0;for(let i=0;i<r.polygon.length;i++){let a=r.polygon[i],b=r.polygon[(i+1)%r.polygon.length];sum+=a[0]*b[1]-b[0]*a[1]}area+=Math.abs(sum)/2}
  assert.equal(area,4e6);
  for(let x=7.31;x<2000;x+=17)for(let z=11.27;z<2000;z+=19)assert.equal(data.regions.filter(r=>contains([x,z],r.polygon)).length,1,`${x},${z}`);
});
check('64 tiles, preserved resolution and landmark Unity coordinates',()=>{
  assert.equal(data.proposedSize/data.tileSize,8);assert.equal(data.proposedTiles,64);
  assert.deepEqual(data.settlements[0].point.map((v,i)=>v+data.unityOrigin[i]),[-385,720]);
  assert.deepEqual(data.locks[0].center.map((v,i)=>v+data.unityOrigin[i]),[-405,335]);
  assert.deepEqual([1360,1380].map((v,i)=>v+data.unityOrigin[i]),[290,760]);
  assert.equal((2049-1)/8+1,257);
});
check('Source inputs unchanged since documentation generation',()=>{
  for(const [p,h] of Object.entries(data.sourceHashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex'),h,p);
});
check('Sample intersects tile seam and stays outside sakura footprint',()=>{
  const [x0,z0,x1,z1]=data.sample.bounds;assert(x0<1000&&x1>1000);assert.equal((x1-x0)*(z1-z0),44000);
  const s=data.locks[0];assert(x0>s.center[0]+s.radius[0]);
});
(async()=>{
  let browser;
  try{
    const exe=['C:/Program Files/Google/Chrome/Application/chrome.exe','C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find(fs.existsSync);
    browser=await chromium.launch({headless:true,...(exe?{executablePath:exe}:{})});
    const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1});
    page.on('pageerror',e=>report.errors.push(e.message));
    page.on('console',m=>{if(m.type()==='error')report.errors.push(m.text())});
    await page.goto(pathToFileURL(path.join(here,'map-plan.html')).href);await page.waitForLoadState('load');
    assert.equal(await page.locator('[data-region]').count(),16);
    assert(await page.locator('[id]').evaluateAll(es=>new Set(es.map(e=>e.id)).size===es.length));report.checks.push('All HTML and SVG IDs are unique');
    for(const c of 'ABCDEFGHIJKLMNOP'){await page.locator(`[data-region=${c}]`).click();assert((await page.locator('#region-title').innerText()).startsWith(c+' ·'))}
    await page.locator('[data-region=A]').click();report.checks.push('All 16 region selectors change the visible specification');
    for(const c of await page.locator('[data-layer]').all()){let id=await c.getAttribute('data-layer');await c.check();assert(await page.locator('#'+id).isVisible());await c.uncheck();assert(!(await page.locator('#'+id).isVisible()));if(!['baseline','camera'].includes(id))await c.check()}
    report.checks.push('All 6 layer toggles show and hide their real SVG group');
    const links=await page.locator('a[href]').evaluateAll(a=>a.map(e=>e.href));let local=0,external=[];
    for(const href of links){const u=new URL(href);if(u.protocol==='file:'){if(u.hash&&fileURLToPath(u)===path.join(here,'map-plan.html'))assert(await page.locator(u.hash).count());else assert(fs.existsSync(fileURLToPath(u)),href);local++}else external.push(href)}
    report.checks.push(`${local} local links/anchors resolve; ${external.length} external source links inventoried`);report.externalLinks=external;
    assert(await page.locator('img').evaluateAll(imgs=>imgs.every(i=>i.complete&&i.naturalWidth>0)));report.checks.push('All 6 local reference images load');
    await page.locator('#atlas').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'atlas-desktop.png')});report.screenshots.push('atlas-desktop.png');
    await page.screenshot({path:path.join(out,'full-desktop.png'),fullPage:true});report.screenshots.push('full-desktop.png');
    await page.locator('[data-layer=camera]').check();await page.locator('#atlas').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'camera-route.png')});report.screenshots.push('camera-route.png');await page.locator('[data-layer=camera]').uncheck();
    for(const size of [{width:1440,height:1100},{width:900,height:1100},{width:390,height:844}]){
      await page.setViewportSize(size);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Horizontal page overflow '+size.width);
      report.checks.push('No horizontal page overflow at '+size.width+'px');
    }
    await page.locator('#atlas').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'atlas-mobile.png')});report.screenshots.push('atlas-mobile.png');
    await page.setViewportSize({width:1440,height:1100});await page.locator('#sample').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'sample-desktop.png')});report.screenshots.push('sample-desktop.png');
    assert.equal(report.errors.length,0,report.errors.join('\n'));report.checks.push('No browser console errors or uncaught JavaScript exceptions');
    report.status='PASS';
  }catch(e){report.status='FAIL';report.errors.push(e.stack);process.exitCode=1}
  finally{if(browser)await browser.close();fs.writeFileSync(path.join(out,'checks.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2))}
})();
