// Bundle a portable offline HTML page, without a server or third-party dependencies.
const fs=require('node:fs'),path=require('node:path');
const read=name=>fs.readFileSync(path.join(__dirname,name),'utf8');
let html=read('index.html').replace('<link rel="stylesheet" href="style.css">',()=>'<style>'+read('style.css')+'</style>');
for(const name of ['catalog.js','app.js'])html=html.replace(`<script src="${name}"></script>`,()=>'<script>'+read(name).replace(/<\/script/gi,'<\\/script')+'</script>');
if(/(?:src|href)="(?:catalog\.js|app\.js|style\.css)"/.test(html))throw Error('Unbundled dependency');
const out=path.join(__dirname,'Serenity-Research-Review.html');fs.writeFileSync(out,html,'utf8');console.log(out);
