const fs = require('fs');
const glob = require('glob');

const replacements = [
    { regex: /(?<!dark:)bg-slate-900(\/[0-9]+)?/g, repl: 'bg-slate-50$1 dark:bg-slate-900$1' },
    { regex: /(?<!dark:)bg-slate-800(\/[0-9]+)?/g, repl: 'bg-white$1 dark:bg-slate-800$1' },
    { regex: /(?<!dark:)bg-slate-700(\/[0-9]+)?/g, repl: 'bg-slate-100$1 dark:bg-slate-700$1' },
    { regex: /(?<!dark:)border-slate-700(\/[0-9]+)?/g, repl: 'border-slate-200$1 dark:border-slate-700$1' },
    { regex: /(?<!dark:)border-slate-600(\/[0-9]+)?/g, repl: 'border-slate-300$1 dark:border-slate-600$1' },
    { regex: /(?<!dark:)text-slate-400(\/[0-9]+)?/g, repl: 'text-slate-500$1 dark:text-slate-400$1' },
    { regex: /(?<!dark:)text-slate-300(\/[0-9]+)?/g, repl: 'text-slate-700$1 dark:text-slate-300$1' },
    { regex: /(?<!dark:)text-slate-500(\/[0-9]+)?/g, repl: 'text-slate-400$1 dark:text-slate-500$1' },
    { regex: /(?<!dark:)text-white/g, repl: 'text-slate-900 dark:text-white' }
];

const files = glob.sync('src/CoEngine.Client/{Pages,Layout}/**/*.razor');

for (const file of files) {
    let content = fs.readFileSync(file, 'utf8');
    let original = content;
    for (const r of replacements) {
        content = content.replace(r.regex, r.repl);
    }
    
    // Fix text-white inside gradient backgrounds or primary buttons which should stay white
    content = content.replace(/linear-gradient(.*?)text-slate-900 dark:text-white/g, 'linear-gradient$1text-white');
    content = content.replace(/bg-gradient-(.*?)text-slate-900 dark:text-white/g, 'bg-gradient-$1text-white');
    
    if (content !== original) {
        fs.writeFileSync(file, content, 'utf8');
        console.log(`Updated ${file}`);
    }
}
