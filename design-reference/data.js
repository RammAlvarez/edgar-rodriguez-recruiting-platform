// Mock data + payroll engine for "Plataforma de Reclutamiento y Prenómina"
const pad = (n, w = 4) => String(n).padStart(w, '0');
export const fmtMoney = n => '$ ' + (Math.round((n || 0) * 100) / 100).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
export const fmtDate = iso => { if (!iso) return ''; const [y, m, d] = iso.split('-'); return `${d}/${m}/${y}`; };
export const isoToday = () => { const d = new Date(); return `${d.getFullYear()}-${pad(d.getMonth() + 1, 2)}-${pad(d.getDate(), 2)}`; };
export const addDays = (iso, n) => { const d = new Date(iso + 'T12:00:00'); d.setDate(d.getDate() + n); return `${d.getFullYear()}-${pad(d.getMonth() + 1, 2)}-${pad(d.getDate(), 2)}`; };
export const daysBetween = (a, b) => Math.round((new Date(b + 'T12:00:00') - new Date(a + 'T12:00:00')) / 86400000);

export const CLIENT_NAMES = ['EL CAMINO', 'GTS', 'FILM', 'ECOVIA', 'NM FIRE', 'LUNACO', 'HEB', 'CERRO DEL FRAILE', 'I CALIZA', 'CONSTRUCTORA HFC', 'JINSEONG TECH'];
export const RECRUITERS = ['Mónica Soto', 'Pricila de León', 'Belem González', 'Servando Moreno', 'Aarón Vázquez', 'Aarón Brandon'];
export const ETAPAS = ['Nuevo', 'Contactado', 'Entrevista', 'Apto', 'No apto', 'Contratado', 'Descartado'];
export const DOCS = ['INE', 'CURP', 'RFC', 'NSS', 'Acta de nacimiento', 'Comprobante de domicilio', 'Estado de cuenta'];
export const FUENTES = ['Facebook', 'LinkedIn', 'WhatsApp', 'Referido', 'Bolsa de trabajo', 'Otro'];
export const BANCOS = ['Banorte', 'Banregio', 'Banco Azteca', 'BBVA', 'Santander'];
export const PRESTACIONES = ['IMSS', 'Vales de despensa', 'Fondo de ahorro', 'Transporte', 'Uniformes', 'Seguro de vida'];
export const CONCEPTOS = ['Horas extra', 'Retroactivo', 'Descanso laborado', 'Prima dominical', 'Festivo laborado', 'Descuento', 'Pensión alimenticia'];
export const RATES = { VALIDADOR: { sdb: 428.57, imss: 278.80, hora: 35.71 }, SUPERVISOR: { sdb: 585.71, imss: 278.80, hora: 48.80 } };
export const PERIODS = [
  { id: 'S38', label: 'Semana 38 · 11–17 sep 2026', short: 'Semana 38', start: '2026-09-11', end: '2026-09-17', festivos: ['2026-09-16'] },
  { id: 'S39', label: 'Semana 39 · 18–24 sep 2026', short: 'Semana 39', start: '2026-09-18', end: '2026-09-24', festivos: [] },
];
export const PERIOD = PERIODS[0];
export const periodOf = iso => PERIODS.find(p => iso >= p.start && iso <= p.end);
export const nextPeriod = p => PERIODS[PERIODS.indexOf(p) + 1] || null;
export const STAGES = ['Capturando', 'Revisión', 'Pagada'];
export const TIPOS_CUENTA = ['Cuenta propia', 'Tarjeta de débito', 'CLABE'];
const norm = s => String(s || '').toUpperCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, ' ').trim();
export const principalDe = e => (e.cuentas || []).find(c => c.principal && !c.hasta) || null;
export const syncAccount = e => { const c = principalDe(e); e.banco = c ? c.banco : ''; e.tipoCuenta = c ? c.tipoCuenta : ''; e.cuenta = c ? c.cuenta : ''; e.titular = c ? c.titular : ''; return e; };
export const esTercero = (e, c) => !!c && norm(c.titular) !== norm(e.nombre);
export const ACCT_LEN = { 'Tarjeta débito': [16], 'Tarjeta de débito': [16], CLABE: [18], 'Cuenta Azteca': [14, 20], 'Cuenta propia': [14, 20] };
export const BANK_CODES = { Banorte: '072', Banregio: '058', 'Banco Azteca': '127', BBVA: '012', Santander: '014' };
export const ACCT_TYPE_CODE = { 'Tarjeta débito': '3', 'Tarjeta de débito': '3', CLABE: '40', 'Cuenta Azteca': '1', 'Cuenta propia': '1' };
export const validAcct = (tipo, num) => { const n = String(num || '').replace(/\D/g, ''); const ok = ACCT_LEN[tipo] || []; return n.length > 0 && ok.includes(n.length); };
export const acctHint = (tipo, num) => { const n = String(num || '').replace(/\D/g, ''); const ok = ACCT_LEN[tipo] || []; if (!tipo) return { text: 'Elige el tipo de cuenta', bad: false }; if (!n.length) return { text: `Debe tener ${ok.join(' o ')} dígitos`, bad: false }; return ok.includes(n.length) ? { text: `${n.length} dígitos · correcto`, bad: false } : { text: `${n.length} de ${ok.join(' o ')} dígitos`, bad: true }; };
export const mask = num => num ? '•••• ' + String(num).slice(-4) : '—';
const digits = (seed, len) => { let s = '', x = seed * 7919 + 17; for (let k = 0; k < len; k++) { x = (x * 1103515245 + 12345) % 2147483648; s += x % 10; } return s; };
// Derived payment state for an employee in a period
export function payState(data, e, periodId) {
  const paid = (data.pagos[periodId] || {})[e.id]; if (paid) return paid;
  if (!e.cuenta) return 'Sin cuenta';
  if (!validAcct(e.tipoCuenta, e.cuenta)) return 'Cuenta inválida';
  return 'Listo';
}
export const SITES = [{ id: 'S1', cliente: 'ECOVIA', nombre: 'ECOVIA — Estación Centro' }, { id: 'S2', cliente: 'ECOVIA', nombre: 'ECOVIA — Estación Norte' }, { id: 'S3', cliente: 'HEB', nombre: 'HEB — Oficinas Cumbres' }, { id: 'S4', cliente: 'CONSTRUCTORA HFC', nombre: 'CONSTRUCTORA HFC — Obra Apodaca' }];
export const MUNICIPIOS_NL = ['Monterrey', 'San Nicolás de los Garza', 'Guadalupe', 'Apodaca', 'General Escobedo', 'Santa Catarina', 'García', 'San Pedro Garza García', 'Juárez', 'Cadereyta Jiménez', 'Santiago', 'Salinas Victoria', 'Pesquería', 'Linares', 'Montemorelos'];
export const WEEKDAYS = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
export function payrollGroups() {
  return [
    { id: 'PG1', cliente: 'ECOVIA', nombre: 'Validadores 12 h', tipo: 'Semanal', corte: 'Viernes', horas: 12, bloqueo: '23:59', estatus: 'Activo' },
    { id: 'PG2', cliente: 'CONSTRUCTORA HFC', nombre: 'Cuadrilla obra', tipo: 'Semanal', corte: 'Sábado', horas: 8, bloqueo: '20:00', estatus: 'Activo' },
    { id: 'PG3', cliente: 'HEB', nombre: 'Limpieza oficinas', tipo: 'Quincenal', corte: 'Viernes', horas: 8, bloqueo: '23:59', estatus: 'Activo' },
  ];
}
export function puestos() {
  return [
    { id: 'PU1', grupoId: 'PG1', cliente: 'ECOVIA', nombre: 'VALIDADOR', sdb: 428.57, imss: 278.80, hora: 35.71, bono: 450 },
    { id: 'PU2', grupoId: 'PG1', cliente: 'ECOVIA', nombre: 'SUPERVISOR', sdb: 585.71, imss: 278.80, hora: 48.80, bono: 600 },
    { id: 'PU3', grupoId: 'PG2', cliente: 'CONSTRUCTORA HFC', nombre: 'ALBAÑIL', sdb: 520.00, imss: 278.80, hora: 65.00, bono: 500 },
    { id: 'PU4', grupoId: 'PG2', cliente: 'CONSTRUCTORA HFC', nombre: 'AYUDANTE GENERAL', sdb: 380.00, imss: 278.80, hora: 47.50, bono: 350 },
    { id: 'PU5', grupoId: 'PG3', cliente: 'HEB', nombre: 'INTENDENTE', sdb: 320.00, imss: 278.80, hora: 40.00, bono: 300 },
  ];
}
export const nextCut = (g, from = isoToday()) => { const target = (WEEKDAYS.indexOf(g.corte) + 1) % 7; let d = from; for (let i = 0; i < 7; i++) { if (new Date(d + 'T12:00:00').getDay() === target) return d; d = addDays(d, 1); } return d; };
export const rateFor = (data, e) => { if (e.tarifas) return { sdb: +e.tarifas.sdb || 0, imss: +e.tarifas.imss || 0, hora: +e.tarifas.hora || 0, bono: +e.tarifas.bono || 0 }; const p = data.puestos.find(p => p.id === e.puestoId); return p ? { sdb: p.sdb, imss: p.imss, hora: p.hora, bono: p.bono } : (RATES[e.puesto] || RATES.VALIDADOR); };

const RAZONES = ['Transportes El Camino SA de CV', 'GTS Logística SA de CV', 'Film Industrias de México S de RL', 'Ecovía Movilidad Metropolitana SA de CV', 'NM Fire Protection SA de CV', 'Lunaco Distribuciones SA de CV', 'Supermercados Internacionales HEB SA de CV', 'Cerro del Fraile Desarrollos SA de CV', 'Industrias Caliza SA de CV', 'Constructora HFC SA de CV', 'Jinseong Tech México S de RL'];
const RFCS = ['TEC980312AB1', 'GTL050614KR7', 'FIM120930QW2', 'EMM140207HN9', 'NFP090823LM4', 'LDI110415ZX8', 'SIH971127PL3', 'CFD160508TY6', 'ICA030219UV5', 'CHF081104GH2', 'JTM190613RD0'];
const ZONAS = ['Monterrey centro', 'San Nicolás', 'Apodaca', 'Guadalupe', 'Escobedo', 'Santa Catarina', 'García'];

export function clients() {
  return CLIENT_NAMES.map((n, i) => ({
    id: 'CLI-' + pad(i + 1, 3), nombre: n, razonSocial: RAZONES[i], rfc: RFCS[i], zona: ZONAS[i % ZONAS.length], diasPromedio: 12 + (i * 7) % 20,
    contactos: [{ nombre: ['Laura Medina', 'Rodrigo Salinas', 'Patricia Garza', 'Héctor Elizondo', 'Ana Cavazos', 'Luis Treviño', 'Gabriela Villarreal', 'Mario Cantú', 'Sofía Leal', 'Daniel Guerra', 'Kim Ji-ho'][i], puesto: 'Gerente de operaciones', principal: true, correo: `contacto@${n.toLowerCase().replace(/\s+/g, '')}.mx`, portal: i === 6 }],
    sitios: [{ nombre: n + ' — Planta ' + ZONAS[i % ZONAS.length], turnos: 'C/DÍA · C/NOCHE' }].concat(i === 3 ? [{ nombre: 'ECOVIA — Estación Centro', turnos: 'C/DÍA · C/NOCHE' }, { nombre: 'ECOVIA — Estación Norte', turnos: 'C/DÍA' }] : []),
  }));
}

export function vacantes() {
  const V = [
    ['ECOVIA', 'VALIDADOR', 'Monterrey centro', 5, 'Alta', 'En proceso', '2026-09-01', 'Mónica Soto', 12857],
    ['ECOVIA', 'SUPERVISOR', 'Monterrey centro', 2, 'Media', 'Abierta', '2026-09-20', 'Mónica Soto', 17571],
    ['HEB', 'AUXILIAR DE PISO', 'San Nicolás', 3, 'Alta', 'En proceso', '2026-09-14', 'Pricila de León', 9800],
    ['HEB', 'CAJERO', 'Guadalupe', 4, 'Media', 'Abierta', '2026-09-28', 'Mónica Soto', 10200],
    ['GTS', 'OPERADOR DE MONTACARGAS', 'Apodaca', 2, 'Baja', 'Cubierta', '2026-08-10', 'Belem González', 13500],
    ['FILM', 'AYUDANTE GENERAL', 'Escobedo', 6, 'Alta', 'En proceso', '2026-09-08', 'Servando Moreno', 9200],
    ['NM FIRE', 'TÉCNICO EN SISTEMAS CONTRA INCENDIO', 'Santa Catarina', 1, 'Media', 'Pausada', '2026-08-25', 'Aarón Vázquez', 16000],
    ['LUNACO', 'VENDEDOR DE MOSTRADOR', 'Monterrey centro', 2, 'Baja', 'Abierta', '2026-09-30', 'Aarón Brandon', 9500],
    ['EL CAMINO', 'CHOFER REPARTIDOR', 'García', 3, 'Alta', 'En proceso', '2026-09-05', 'Mónica Soto', 12000],
    ['CERRO DEL FRAILE', 'GUARDIA DE SEGURIDAD', 'Santa Catarina', 4, 'Media', 'Abierta', '2026-09-22', 'Belem González', 10800],
    ['CONSTRUCTORA HFC', 'ALBAÑIL', 'Apodaca', 8, 'Baja', 'Cancelada', '2026-07-15', 'Servando Moreno', 11000],
    ['JINSEONG TECH', 'OPERADOR DE PRODUCCIÓN', 'Apodaca', 10, 'Alta', 'En proceso', '2026-09-10', 'Pricila de León', 11400],
    ['HEB', 'CAJERO', 'San Nicolás', 2, 'Baja', 'Abierta', '2026-10-02', 'Belem González', 10200],
  ];
  return V.map((r, i) => ({
    id: 'VAC-' + pad(i + 1), cliente: r[0], puesto: r[1], zona: r[2], solicitadas: r[3], prioridad: r[4], estatus: r[5], abierta: r[6], reclutador: r[7], sueldo: r[8],
    prestaciones: ['IMSS', 'Vales de despensa'], horario: '12 h · 6×1', bancos: ['Banorte', 'Banco Azteca'], estudios: 'Secundaria', experiencia: 1, expReq: 'Atención al público', sexo: 'Indistinto', edad: '18–45', observaciones: '',
    docsRequeridos: DOCS,
  }));
}

const CAND_NAMES = ['Juan Ramírez Torres', 'María Fernanda López Cruz', 'José Luis Hernández Peña', 'Alejandra Martínez Ríos', 'Carlos Alberto García Luna', 'Daniela Rodríguez Salas', 'Miguel Ángel Pérez Ortiz', 'Valeria Sánchez Mata', 'Ricardo Flores Ibarra', 'Andrea Gómez Treviño', 'Fernando Díaz Reyes', 'Lucía Torres Aguilar', 'Jorge Morales Castillo', 'Paola Ruiz Mendoza', 'Eduardo Jiménez Vega', 'Karla Ramos Delgado', 'Roberto Castro Núñez', 'Mariana Ortega Silva', 'Alberto Vargas Molina', 'Gabriela Mendoza Rangel', 'Héctor Guerrero Campos', 'Sandra Chávez Ponce', 'Raúl Navarro Cortés', 'Diana Romero Zúñiga', 'Óscar Medina Acosta', 'Jessica Herrera Lozano', 'Arturo Domínguez Ríos', 'Claudia Espinoza Villa', 'Iván Cárdenas Soto', 'Brenda Alvarado Fuentes'];
// [vacIndex(1-based), etapa, fuente]
const CAND_MAP = [[3, 'Entrevista', 'Facebook'], [3, 'Nuevo', 'LinkedIn'], [3, 'Contactado', 'Referido'], [3, 'Apto', 'Bolsa de trabajo'], [3, 'Contratado', 'Facebook'], [3, 'No apto', 'WhatsApp'],
  [4, 'Nuevo', 'Facebook'], [4, 'Contactado', 'LinkedIn'], [4, 'Entrevista', 'Referido'], [4, 'Apto', 'Bolsa de trabajo'],
  [1, 'Nuevo', 'Facebook'], [1, 'Contactado', 'Otro'], [1, 'Entrevista', 'LinkedIn'], [1, 'Apto', 'Referido'], [1, 'Contratado', 'Facebook'], [1, 'Contratado', 'Bolsa de trabajo'], [1, 'Descartado', 'WhatsApp'],
  [2, 'Nuevo', 'LinkedIn'], [2, 'Entrevista', 'Referido'],
  [9, 'Contactado', 'Facebook'], [9, 'Apto', 'Referido'], [9, 'Contratado', 'LinkedIn'],
  [6, 'Nuevo', 'WhatsApp'], [6, 'Entrevista', 'Facebook'], [6, 'Apto', 'Bolsa de trabajo'], [6, 'No apto', 'LinkedIn'],
  [12, 'Nuevo', 'Otro'], [12, 'Contactado', 'Facebook'], [12, 'Entrevista', 'Referido'], [10, 'Nuevo', 'Facebook']];
const DOC_PATTERNS = [
  { INE: 'Validado', CURP: 'Validado', RFC: 'Entregado', NSS: 'Rechazado', 'Acta de nacimiento': 'Pendiente', 'Comprobante de domicilio': 'Validado', 'Estado de cuenta': 'Pendiente' },
  { INE: 'Entregado', CURP: 'Pendiente', RFC: 'Pendiente', NSS: 'Pendiente', 'Acta de nacimiento': 'Pendiente', 'Comprobante de domicilio': 'Pendiente', 'Estado de cuenta': 'Pendiente' },
  { INE: 'Validado', CURP: 'Validado', RFC: 'Validado', NSS: 'Validado', 'Acta de nacimiento': 'Validado', 'Comprobante de domicilio': 'Entregado', 'Estado de cuenta': 'Entregado' },
  { INE: 'Validado', CURP: 'Validado', RFC: 'Validado', NSS: 'Validado', 'Acta de nacimiento': 'Validado', 'Comprobante de domicilio': 'Validado', 'Estado de cuenta': 'Validado' },
];
export function candidatos(vacs) {
  return CAND_NAMES.map((n, i) => {
    const [vi, etapa, fuente] = CAND_MAP[i]; const v = vacs[vi - 1];
    const full = ['Contratado', 'Apto'].includes(etapa) ? 3 : etapa === 'Entrevista' ? 2 : etapa === 'Nuevo' ? 1 : 0;
    return {
      id: 'CAN-' + pad(i + 1), nombre: n, telefono: '81' + String(10000000 + ((i * 7919) % 89999999)).padStart(8, '0'), vacId: v.id, reclutador: v.reclutador, fuente, etapa,
      dias: 3 + (i * 5) % 25, sueldo: v.sueldo, curp: curpFor(n, i), fechaEntrevista: ['Entrevista', 'Apto', 'Contratado', 'No apto'].includes(etapa) ? addDays('2026-09-20', i % 12) : '',
      docs: { ...DOC_PATTERNS[i === 0 ? 0 : full] }, rechazoMotivo: { NSS: 'Imagen ilegible' }, convertido: false,
      correo: n.split(' ')[0].toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') + i + '@gmail.com', calle: 'Av. Lincoln', numero: String(100 + i * 13), colonia: 'Mitras Norte', cp: '64320', municipio: 'Monterrey', estado: 'Nuevo León', fechaNac: `${1985 + (i % 18)}-0${1 + (i % 9)}-1${i % 9}`,
      apellidoP: n.split(' ').slice(-2)[0], apellidoM: n.split(' ').slice(-1)[0], nombres: n.split(' ').slice(0, -2).join(' '),
    };
  });
}
function curpFor(n, i) { const p = n.split(' '); return (p[p.length - 2][0] + 'A' + p[p.length - 1][0] + p[0][0]).toUpperCase() + String(850101 + i * 10101).slice(0, 6) + (i % 2 ? 'H' : 'M') + ((i === 17 || i === 57) ? 'NE' : 'NL') + 'RRT' + pad(i, 2); }

const EXTRA_EMP = [['Tomás Reyna Garza', 'CONSTRUCTORA HFC', 'PU3'], ['Marco Antonio Lira Peña', 'CONSTRUCTORA HFC', 'PU3'], ['Ismael Coronado Vela', 'CONSTRUCTORA HFC', 'PU3'], ['Javier Olvera Ríos', 'CONSTRUCTORA HFC', 'PU4'], ['Noé Alanís Treviño', 'CONSTRUCTORA HFC', 'PU4'], ['Samuel Orozco Mata', 'CONSTRUCTORA HFC', 'PU4'], ['Cristian Bernal Ayala', 'CONSTRUCTORA HFC', 'PU4'], ['Ángel Zapata Cruz', 'CONSTRUCTORA HFC', 'PU4'], ['Guadalupe Cantú Flores', 'HEB', 'PU5'], ['Leticia Aguirre Soto', 'HEB', 'PU5'], ['Yolanda Pacheco Luna', 'HEB', 'PU5'], ['Raquel Esquivel Mora', 'HEB', 'PU5']];
const EMP_NAMES = ['Pedro Antonio Salazar Ruiz', 'Rosa María Cervantes Lara', 'Luis Enrique Padilla Mora', 'Ana Karen Bautista Ríos', 'Francisco Javier Ledezma Cruz', 'Verónica Montes Arriaga', 'Manuel Alejandro Quiroga Pérez', 'Estefanía Carrillo Núñez', 'Jesús Armando Rosales Vidal', 'Norma Angélica Tapia Luna', 'Gerardo Villanueva Ochoa', 'Lorena Ávila Zamora', 'Hugo César Barrera Solís', 'Adriana Estrada Peña', 'Víctor Hugo Nava Ramírez', 'Mónica Patricia Rivas León', 'Julio César Macías Ortega', 'Elena Sandoval Méndez', 'Rubén Darío Cortez Galván', 'Patricia Ibarra Valdez'];
const CARD_QUEUE = { 0: 'Banorte', 2: 'Banorte', 5: 'Banorte', 12: 'Banorte', 11: 'Banregio', 16: 'Banregio' };
const TERCEROS = { 1: 'JOSE ALFREDO CERVANTES LARA', 4: 'LETICIA MORA SANCHEZ', 6: 'MARIA ELENA TORRES GARCIA' };
function account(i, nombre) {
  const T = norm(nombre); const since = addDays('2025-02-03', i * 23); const base = { cuentaRegistrada: i !== 9, tarjetaSolicitada: CARD_QUEUE[i] || null };
  if (i === 12 || i === 16 || i === 18) return { ...base, cuentas: [], cuentaRegistrada: false, tarjetaSolicitada: i === 18 ? null : base.tarjetaSolicitada };
  if (i === 3) return { ...base, cuentas: [{ id: 'CTA-' + i + 'a', alias: 'CLABE BBVA', banco: 'BBVA', tipoCuenta: 'CLABE', cuenta: digits(i, 17), titular: T, desde: since, hasta: '', principal: true }] };
  const banco = BANCOS[i % 3 === 0 ? 3 : i % 3]; const tipo = banco === 'BBVA' ? 'CLABE' : 'Tarjeta de débito';
  const cuentas = [{ id: 'CTA-' + i + 'a', alias: (tipo === 'CLABE' ? 'CLABE ' : 'Tarjeta ') + banco, banco, tipoCuenta: tipo, cuenta: digits(i, tipo === 'CLABE' ? 18 : 16), titular: T, desde: since, hasta: '', principal: i !== 6 }];
  if (TERCEROS[i]) cuentas.push({ id: 'CTA-' + i + 'b', alias: 'Cuenta familiar', banco: 'Banco Azteca', tipoCuenta: 'Cuenta propia', cuenta: digits(i + 50, 14), titular: TERCEROS[i], desde: addDays(since, 120), hasta: '', principal: i === 6 });
  return { ...base, cuentas };
}
export function empleados() {
  const P = puestos();
  return [...EMP_NAMES.map((n, i) => [n, 'ECOVIA', i % 4 === 3 ? 'PU2' : 'PU1']), ...EXTRA_EMP].map(([n, cliente, puestoId], i) => {
    const pu = P.find(x => x.id === puestoId); const p = n.split(' ');
    return syncAccount({
      id: 'EMP-' + pad(i + 1, 3), nombre: n, nombres: p.slice(0, -2).join(' '), apellidoP: p[p.length - 2], apellidoM: p[p.length - 1], cliente, grupoId: pu.grupoId, puestoId, puesto: pu.nombre, turno: i % 3 === 2 ? 'C/NOCHE' : 'C/DÍA', tarifas: null, asigDesde: addDays('2025-02-03', i * 23), historial: [],
      ingreso: addDays('2025-02-03', i * 23), estatus: i === 7 ? 'Incapacidad' : i === 14 ? 'Baja' : [1, 10].includes(i) ? 'Vacaciones' : 'Activo', periodo: i === 7 ? { tipo: 'Incapacidad', inicio: '2026-09-10', fin: '2026-10-15', folio: 'ST-7 448120' } : i === 1 ? { tipo: 'Vacaciones', inicio: '2026-10-06', fin: '2026-10-16' } : i === 10 ? { tipo: 'Vacaciones', inicio: '2026-10-14', fin: '2026-10-20' } : null, estatusHist: i === 7 ? [{ estatus: 'Incapacidad', desde: '2026-09-10', hasta: '2026-10-15', motivo: 'Folio IMSS ST-7 448120', capturo: 'Edgar' }] : i === 1 ? [{ estatus: 'Vacaciones', desde: '2026-10-06', hasta: '2026-10-16', motivo: 'Vacaciones programadas', capturo: 'Edgar' }] : i === 10 ? [{ estatus: 'Vacaciones', desde: '2026-10-14', hasta: '2026-10-20', motivo: 'Vacaciones programadas', capturo: 'Claudia Benavides' }] : i === 14 ? [{ estatus: 'Baja', desde: '2026-09-02', hasta: '', motivo: 'Renuncia voluntaria', capturo: 'Edgar' }] : [], bajaFecha: i === 14 ? '2026-09-02' : '', bajaMotivo: i === 14 ? 'Renuncia voluntaria' : '', ...account(i, n),
      siteId: cliente === 'HEB' ? 'S3' : cliente === 'CONSTRUCTORA HFC' ? 'S4' : i < 12 ? 'S1' : 'S2', docs: 70 + (i * 13) % 31, curp: curpFor(n, i + 40), rfc: (p[p.length - 2].slice(0, 2) + p[p.length - 1][0] + p[0][0]).toUpperCase() + String(880101 + i * 20202).slice(0, 6) + 'K' + String(i).padStart(2, '0'),
      nss: '2' + String(1234567890 + i * 98765).slice(0, 10), celular: i === 5 ? '5555555555' : '81' + String(20000000 + i * 1234567).slice(0, 8), correo: p[0].toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') + '.' + p[p.length - 2].toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') + '@gmail.com',
      calle: ['Av. Ruiz Cortines', 'Calle Hidalgo', 'Av. Lincoln', 'Calle Morelos', 'Av. Universidad'][i % 5], numero: String(200 + i * 17), colonia: ['Mitras Centro', 'Anáhuac', 'Cumbres', 'San Bernabé', 'Valle Verde'][i % 5], cp: String(64000 + i * 20), municipio: 'Monterrey', estado: 'Nuevo León',
      fechaNac: `${1980 + (i % 22)}-${pad(1 + (i % 12), 2)}-${pad(1 + (i % 27), 2)}`, genero: i % 2 ? 'F' : 'M', estadoNac: i === 17 ? 'Extranjero' : 'Nuevo León', tarjeta: '4152 3138 ' + pad(1000 + i * 37) + ' ' + pad(2000 + i * 53), sucursal: '0' + (100 + i), pension: i === 9, convertido: false,
    });
  });
}

// Attendance seed: {date:{empId:{s:'P'|'X'|'F'|'D'|'H'|'I', h?:number}}}
export function attendanceFor(emps, start, end, variant = 0) {
  const a = {};
  for (let d = start; d <= end; d = addDays(d, 1)) {
    a[d] = {};
    emps.forEach((e, i) => {
      if (e.estatus === 'Baja') return;
      const dow = new Date(d + 'T12:00:00').getDay(); const dn = daysBetween(start, d);
      let s = 'P';
      if (e.periodo && d >= e.periodo.inicio && d <= e.periodo.fin) { a[d][e.id] = { s: e.periodo.tipo === 'Vacaciones' ? 'V' : e.periodo.tipo === 'Suspensión' ? 'S' : 'I' }; return; }
      if (e.estatus !== 'Activo') return;
      if (d === '2026-09-16') s = 'H';
      else if (dow === (i % 6) + 1 && dow !== 0) s = 'D';
      else if (variant === 0 && i === 2 && d === '2026-09-12') s = 'F';
      else if (variant === 0 && i === 4 && d === '2026-09-14') s = 'X';
      else if (variant > 0 && (i + variant) % 7 === 0 && dn === (i % 5)) s = 'F';
      else if (variant > 0 && (i + variant) % 9 === 0 && dn === 3) s = 'X';
      else if (i === 7) s = 'I';
      a[d][e.id] = s === 'X' ? { s, h: 8 } : { s };
    });
  }
  return a;
}
export function attendance(emps) { return attendanceFor(emps, PERIODS[0].start, PERIODS[PERIODS.length - 1].end, 0); }
// Week helper: S38 = 11–17 sep 2026 (viernes–jueves)
const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];
export function weekP(n) { const start = addDays('2026-09-11', (n - 38) * 7); const end = addDays(start, 6); const [y, m1, d1] = start.split('-'); const [, m2, d2] = end.split('-'); const rango = m1 === m2 ? `${d1}–${d2} ${MESES[+m2 - 1]} ${y}` : `${d1} ${MESES[+m1 - 1]}–${d2} ${MESES[+m2 - 1]} ${y}`; return { id: 'S' + n, n, start, end, short: 'Semana ' + n, label: `Semana ${n} · ${rango}`, festivos: n === 38 ? ['2026-09-16'] : [] }; }
const cap = s => { const x = String(s || '').toLowerCase(); return x.charAt(0).toUpperCase() + x.slice(1); };
export function vacName(v, all = []) { if (!v) return ''; const open = x => ['Abierta', 'En proceso', 'Pausada'].includes(x.estatus); const dup = open(v) && all.some(x => x.id !== v.id && x.cliente === v.cliente && x.puesto === v.puesto && open(x)); return `${v.cliente} – ${cap(v.puesto)}` + (dup ? ` · ${fmtDate(v.abierta)}` : ''); }
export const weekOfDate = iso => 38 + Math.floor(daysBetween('2026-09-11', iso) / 7);

export function incidencias() {
  return [
    { id: 'INC-001', empId: 'EMP-001', fecha: '2026-09-12', concepto: 'Horas extra', cantidad: 2, importe: 71.42, motivo: 'Cobertura de turno', capturo: 'Edgar' },
    { id: 'INC-002', empId: 'EMP-004', fecha: '2026-09-13', concepto: 'Descanso laborado', cantidad: 1, importe: 585.71, motivo: 'Operativo especial', capturo: 'Edgar' },
    { id: 'INC-003', empId: 'EMP-006', fecha: '2026-09-15', concepto: 'Descuento', cantidad: 1, importe: 300, motivo: 'Préstamo de uniforme', capturo: 'Mónica Soto' },
    { id: 'INC-004', empId: 'EMP-010', fecha: '2026-09-11', concepto: 'Pensión alimenticia', cantidad: 39, importe: 0, motivo: 'Oficio juzgado 3º familiar', capturo: 'Edgar' },
    { id: 'INC-005', empId: 'EMP-011', fecha: '2026-09-16', concepto: 'Festivo laborado', cantidad: 1, importe: 857.14, motivo: 'Independencia', capturo: 'Edgar' },
  ];
}

export function users() {
  return [
    { id: 'U1', nombre: 'Edgar Rodríguez', correo: 'edgar@maxima.mx', rol: 'Administrador', cliente: '', mfa: true, ultimo: '2026-10-07 08:12', activo: true, demo: true, permisos: { 'Reportes de reclutamiento': true, Prenómina: true, Asistencia: true, Incidencias: true, Personal: true, 'Archivos de pago': true, 'Solicitud de tarjetas': true, Usuarios: true }, soloCliente: '' },
    { id: 'U2', nombre: 'Claudia Benavides', correo: 'claudia@maxima.mx', rol: 'Administrador', cliente: '', mfa: true, ultimo: '2026-10-06 17:40', activo: true, permisos: { 'Reportes de reclutamiento': true, Prenómina: false, Asistencia: true, Incidencias: true, Personal: true, 'Archivos de pago': false, 'Solicitud de tarjetas': false, Usuarios: false }, soloCliente: 'ECOVIA' },
    ...RECRUITERS.map((r, i) => ({ id: 'U' + (3 + i), nombre: r, correo: r.split(' ')[0].toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') + '@maxima.mx', rol: 'Reclutador', cliente: '', mfa: i % 2 === 0, ultimo: '2026-10-0' + (1 + i % 6) + ' 09:' + pad(10 + i * 7, 2), activo: i !== 5 })),
    { id: 'U9', nombre: 'Gabriela Villarreal', correo: 'gvillarreal@heb.com.mx', rol: 'Cliente', cliente: 'HEB', mfa: false, ultimo: '2026-10-05 12:03', activo: true },
    { id: 'U10', nombre: 'Héctor Elizondo', correo: 'helizondo@ecovia.mx', rol: 'Cliente', cliente: 'ECOVIA', mfa: true, ultimo: '2026-09-30 16:20', activo: true },
  ];
}

export function comments() {
  return {
    'VAC-0003': [{ autor: 'Gabriela Villarreal (HEB)', fecha: '2026-09-29', texto: 'Necesitamos cubrir las 3 posiciones antes del 15 de octubre.' }, { autor: 'Pricila de León', fecha: '2026-09-30', texto: 'Tenemos 2 candidatos en entrevista esta semana.' }],
    'VAC-0004': [{ autor: 'Gabriela Villarreal (HEB)', fecha: '2026-10-02', texto: 'Preferimos perfiles con experiencia en caja.' }],
    'VAC-0001': [{ autor: 'Héctor Elizondo (ECOVIA)', fecha: '2026-09-25', texto: 'El turno de noche es prioridad.' }],
  };
}

export function initialData() {
  const vacs = vacantes(); const emps = empleados();
  return { clients: clients(), vacantes: vacs, candidatos: candidatos(vacs), empleados: emps, attendance: attendance(emps), unlocked: {}, incidencias: incidencias(), users: users(), comments: comments(), adjustments: {}, periodStatus: { S38: 0, S39: 0 }, pagos: {}, payFiles: {}, cardBatches: [], deposito: {}, seqFile: 1, seqCta: 100, rates: JSON.parse(JSON.stringify(RATES)), payrollGroups: payrollGroups(), puestos: puestos(), seqVac: 14, seqCan: 31, seqEmp: 33, seqInc: 6, seqPG: 4, seqPU: 6, seqCli: 12 };
}

// ---------- Payroll engine ----------
export function computePayroll(data, period = PERIOD, groupId = null) {
  const emps = data.empleados.filter(e => e.estatus !== 'Baja' && (groupId ? e.grupoId === groupId : true));
  const rows = emps.map(e => {
    const rate = rateFor(data, e);
    let dias = 0, festivo = 0, hrsInc = 0, domingo = false;
    for (let d = period.start; d <= period.end; d = addDays(d, 1)) {
      const r = (data.attendance[d] || {})[e.id]; if (!r) continue;
      if (r.s === 'P') { dias++; if (new Date(d + 'T12:00:00').getDay() === 0) domingo = true; }
      else if (r.s === 'X') { dias++; hrsInc += Math.max(0, 12 - (r.h || 0)); }
      else if (r.s === 'H') { festivo++; dias++; }
    }
    const inc = data.incidencias.filter(i => i.empId === e.id && i.fecha >= period.start && i.fecha <= period.end && !i.retroTo);
    const retroInc = data.incidencias.filter(i => i.empId === e.id && i.retroTo === period.id);
    const sum = (c, f) => inc.filter(i => i.concepto === c).reduce((a, i) => a + f(i), 0);
    const pension = e.pension || inc.some(i => i.concepto === 'Pensión alimenticia');
    const base = {
      sueldo: rate.sdb * dias, septimo: rate.sdb * dias / 6, prima: domingo || sum('Prima dominical', () => 1) ? rate.sdb * 0.25 : 0,
      hrsInc: hrsInc * rate.hora, fest1: festivo ? 4 * rate.hora * 3 : 0, hrsExtra: sum('Horas extra', i => i.cantidad * rate.hora), retro: sum('Retroactivo', i => i.importe) + retroInc.reduce((a, i) => a + (i.concepto === 'Descuento' ? -i.importe : i.importe), 0),
      descLab: sum('Descanso laborado', i => i.cantidad * rate.sdb), ted: 0, festLab: sum('Festivo laborado', i => rate.sdb * i.cantidad * 2), descuento: sum('Descuento', i => i.importe),
    };
    const adj = data.adjustments[e.id] || {};
    const src = {}; Object.keys(base).forEach(k => { src[k] = adj[k] ? 'Ajuste manual' : ['sueldo', 'septimo', 'prima', 'hrsInc', 'fest1'].includes(k) ? 'Asistencia' : 'Incidencia'; if (adj[k]) base[k] = adj[k].amount; });
    const bruto = base.sueldo + base.septimo + base.prima + base.fest1 + base.hrsExtra + base.retro + base.descLab + base.ted + base.festLab;
    const neto = bruto - base.hrsInc - base.descuento;
    const pensionImp = pension ? neto * 0.39 : 0;
    return { emp: e, rate, dias, festivo, retroNote: retroInc.length ? 'corresponde a ' + [...new Set(retroInc.map(i => (PERIODS.find(p => p.id === i.retroFrom) || {}).short))].join(', ') : '', hrsIncH: hrsInc, hrsExtraH: sum('Horas extra', i => i.cantidad), ...base, src, bruto, neto, pension, pensionImp, total: neto - pensionImp, comentarios: (adj.__c || inc.map(i => i.motivo)[0] || '') };
  });
  const totals = {}; ['dias', 'sueldo', 'septimo', 'prima', 'hrsInc', 'fest1', 'hrsExtra', 'retro', 'descLab', 'ted', 'festLab', 'descuento', 'total'].forEach(k => totals[k] = rows.reduce((a, r) => a + r[k], 0));
  return { rows, totals };
}

export function validateEmp(e) {
  const flags = [];
  if (/^(\d)\1{9}$/.test(e.celular)) flags.push('Celular con dígitos repetidos');
  if (e.curp.slice(11, 13) === 'NE') flags.push('CURP de extranjero');
  if (!e.correo || !e.correo.includes('@')) flags.push('Correo inválido');
  return flags;
}
