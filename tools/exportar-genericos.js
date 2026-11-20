// Exporta las definiciones de catálogos y formularios de configuración del frontend (retail-web) a
// src/NexoRh.Api/Data/genericos.json, que la API usa para validar y para los datos iniciales.
// Uso: node tools/exportar-genericos.js [ruta del frontend, por defecto ../retail-web]
const fs = require('fs');
const os = require('os');
const path = require('path');

const front = path.resolve(process.argv[2] || path.join(__dirname, '..', '..', 'retail-web'));
const ts = require(path.join(front, 'node_modules', 'typescript'));
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'genericos-'));
for (const f of ['genericos', 'models', 'sample/catalogos-config', 'sample/config-forms', 'sample/asistencia']) {
  const src = fs.readFileSync(path.join(front, 'src/app/core', f + '.ts'), 'utf8');
  const js = ts.transpileModule(src, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 } }).outputText;
  const dest = path.join(tmp, f + '.js');
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.writeFileSync(dest, js);
}
const { CATALOGOS } = require(path.join(tmp, 'sample/catalogos-config.js'));
const { CONFIG_FORMS } = require(path.join(tmp, 'sample/config-forms.js'));

const campoCat = (c) => ({
  key: c.key, label: c.label, tipo: c.tipo, opciones: c.opciones ?? null, obligatorio: !!c.obligatorio, requeridoSi: c.requeridoSi ?? null,
  unico: !!c.unico, patron: c.patron ?? null, min: c.min ?? null, max: c.max ?? null,
});
const salida = {
  generado: 'tools/exportar-genericos.js desde retail-web/src/app/core (catalogos-config.ts y config-forms.ts)',
  catalogos: Object.values(CATALOGOS).map((c) => ({
    clave: c.clave, singular: c.singular, campos: c.campos.map(campoCat), filas: c.filas, referenciadoPor: c.referenciadoPor ?? [],
  })),
  formularios: Object.values(CONFIG_FORMS).map((f) => ({
    clave: f.clave,
    campos: f.tabs.flatMap((t) => t.secciones.flatMap((s) => s.campos)).map((c) => ({
      key: c.key, label: c.label, tipo: c.tipo, opciones: c.opciones ?? null, min: c.min ?? null, max: c.max ?? null,
      obligatorio: !!c.obligatorio, dependeDe: c.dependeDe ?? null,
    })),
    valores: f.valores,
  })),
};
const destino = path.join(__dirname, '..', 'src', 'NexoRh.Api', 'Data', 'genericos.json');
fs.writeFileSync(destino, JSON.stringify(salida, null, 2) + '\n');
fs.rmSync(tmp, { recursive: true, force: true });
console.log(`${salida.catalogos.length} catálogos y ${salida.formularios.length} formularios → ${destino}`);
