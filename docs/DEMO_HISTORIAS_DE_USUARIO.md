# Historias de usuario — Demo de Reclutamiento

Guion para la primera presentación al cliente. **Solo Reclutamiento**: la Nómina no se muestra (D101, son negocios
separados). Cada historia trae sus criterios de aceptación como casillas ☐ para hacer el ensayo en DEV antes de la demo.

Fuentes: `docs/SPEC.md` §2, §4, §6, §7 y `docs/GUIA_DE_USO.md` §3–4.

| App | DEV |
|---|---|
| Administración | https://rass-project-2-admin.web.app |
| Candidatos | https://rass-project-2-candidatos.web.app |
| Clientes | https://rass-project-2-clientes.web.app |

---

## 0. Preparación del ensayo

- ☐ Cuatro correos que lleguen a su buzón: `…+reclutador@`, `…+cliente1@`, `…+candidato1@`, `…+candidato2@`.
- ☐ Una ventana normal (Administrador), una de incógnito (Reclutador) y, si es posible, otro navegador o perfil
  (Cliente / Candidato). Cada sesión ocupa su propia ventana.
- ☐ Un PDF y un JPG de prueba para los documentos del candidato (sin datos reales).
- ☐ **Presente con un Administrador «Demo» (recomendado):** invítelo con los permisos *Reclutamiento*, *Reportes de
  reclutamiento* y *Usuarios* (sin *Usuarios* no puede invitar al contacto del cliente ni al reclutador: HU-A4 y
  HU-A5 dicen «No tienes permiso de «Usuarios»»). Su menú no muestra Nómina y su *Inicio* no tiene los indicadores de
  nómina (empleados, asistencia, prenómina); la cuenta del dueño sí los muestra.
- ☐ Los nombres y datos de esta guía son de ejemplo: use datos ficticios, no de personas reales.

**Datos de ejemplo** (ajústelos a su gusto): cliente **HEB** · sitio *Monterrey Centro* · vacante
*HEB – Auxiliar de limpieza*, 2 posiciones · reclutadora **Mónica Soto** (ficticia) · candidatos **Juan Pérez** y
**Ana López** (ficticios).

---

## 1. Administrador — primer acceso y configuración

> **Como** dueño de la agencia, **quiero** entrar por primera vez y dejar listo todo lo necesario (clientes,
> reclutadores, vacantes y candidatos), **para que** mi equipo empiece a trabajar el mismo día.

### HU-A1 · Entrar por primera vez y proteger la cuenta
**Como** Administrador, **quiero** entrar con mi correo y contraseña y activar la verificación en dos pasos, **para**
proteger los datos personales de los candidatos.

- ☐ Una contraseña equivocada muestra un mensaje de error en español.
- ☐ Con la contraseña correcta veo el menú lateral (Reclutamiento y Administración; Nómina solo si mi usuario la tiene).
- ☐ *Mi perfil → Verificación en dos pasos → Generar código QR*: lo escaneo, escribo el código de 6 dígitos y recibo
  códigos de recuperación.
- ☐ Al cerrar sesión y volver a entrar, me pide el código.

### HU-A2 · Ajustar catálogos
**Como** Administrador, **quiero** revisar y completar los catálogos, **para que** las vacantes usen mis propios
términos.

- ☐ *Administración → Catálogos*: agrego una prestación («Bono de asistencia») y aparece en la lista.
- ☐ Agregarla otra vez muestra «Ya existe una prestación con ese nombre.»
- ☐ Reviso Tipos de documento y la plantilla «Candidato — estándar» (INE, CURP, comprobante de domicilio, …).

### HU-A3 · Dar de alta un cliente
**Como** Administrador, **quiero** registrar a un cliente con sus sitios y contactos, **para** abrirle vacantes.

- ☐ *Reclutamiento → Clientes → Nuevo cliente*: nombre corto, razón social, RFC, contacto principal y celular →
  «Cliente creado». No me pregunta si es de reclutamiento o nómina.
- ☐ Pestaña *Sitios → Nuevo sitio* con zona y código postal → «Sitio guardado».
- ☐ El contacto principal se crea junto con el cliente: la pestaña *Contactos* ya lo muestra. «Nuevo contacto» es
  solo para un segundo contacto.

### HU-A4 · Invitar al contacto del cliente al portal
**Como** Administrador, **quiero** dar acceso al portal a un contacto del cliente, **para que** vea el avance de sus
vacantes sin llamarme.

- ☐ En el contacto, **Invitar al portal** → queda «Con acceso al portal».
- ☐ El contacto aparece como usuario **Cliente** en *Administración → Usuarios*, ligado a HEB.
- ☐ Llega el correo «Tu acceso al Portal de clientes» a `+cliente1` (revise Spam/Promociones). No menciona nómina.

### HU-A5 · Invitar a un reclutador
**Como** Administrador, **quiero** invitar a una reclutadora, **para** asignarle vacantes.

- ☐ *Administración → Usuarios → Invitar usuario*, rol **Reclutador**, correo `+reclutador` → «Invitación enviada a …».
- ☐ La persona aparece en *Reclutamiento → Reclutadores*.
- ☐ En incógnito, el correo de invitación permite definir la contraseña (ver HU-R1).

### HU-A6 · Crear una vacante
**Como** Administrador, **quiero** capturar la vacante tal como la pide el cliente, **para** empezar a buscar candidatos.

- ☐ *Reclutamiento → Vacantes → Nueva vacante*: cliente HEB, puesto, zona, 2 vacantes solicitadas, sueldo,
  prestaciones (incluida «Bono de asistencia»), horario, banco de pago, documentos requeridos, sexo y edad.
- ☐ Marco **Visible en el portal del cliente** → «Vacante creada», estatus *Abierta*.
- ☐ Se muestra como «HEB – Auxiliar de limpieza» (nunca un código) y también aparece en la pestaña *Vacantes* del
  cliente.
- ☐ Alternativa: desde el cliente, *Vacantes → Nueva vacante* abre el mismo formulario con el cliente ya fijo.

### HU-A7 · Asignar la vacante al reclutador
**Como** Administrador, **quiero** asignar la vacante a una reclutadora como líder, **para que** sea responsable de
cubrirla.

- ☐ *Reclutadores → Mónica → Asignar vacante*: elijo la vacante como **Líder**.
- ☐ La vacante aparece en *Vacantes asignadas* con acciones *Hacer líder* / *Quitar*.
- ☐ La reclutadora recibe en la campana «Te asignaron … como líder».
- ☐ La actividad de la vacante registra la asignación.

### HU-A8 · Capturar el primer candidato
**Como** Administrador, **quiero** capturar candidatos yo mismo cuando haga falta, **para** no frenar el proceso.

- ☐ *Vacante → Pipeline → Nuevo candidato*: persona nueva con correo `+candidato1`, fuente, sueldo ofrecido.
- ☐ La tarjeta aparece en **Nuevo** y la vacante pasa a *En proceso*.
- ☐ Llega al candidato el correo «Tu postulación: …».
- ☐ Si capturo otra persona con la misma CURP/RFC, aparece aviso de duplicado.

### HU-A9 · Cubrir a un reclutador (Ver como reclutador)
**Como** Administrador, **quiero** ver y operar el espacio de un reclutador cuando falta, **para** que sus candidatos
no se queden detenidos.

- ☐ *Reclutadores → Mónica → Ver como reclutador*: aparece el aviso «Viendo como Mónica Soto · estás cubriendo su
  trabajo; lo que hagas queda registrado a tu nombre · Salir».
- ☐ Recargar la página o escribir una dirección sigue en el modo cubrir (el aviso no desaparece).
- ☐ Solo veo sus vacantes y candidatos.
- ☐ Una nota escrita en este modo dice «(cubriendo a Mónica Soto)».
- ☐ **Salir** me regresa a mi vista completa.

### HU-A10 · Cerrar la vacante y medir resultados
**Como** dueño, **quiero** ver los reportes de reclutamiento, **para** medir a mis reclutadores y clientes.

- ☐ Al contratar las 2 posiciones, la vacante queda *Cubierta* con fecha y días abierta.
- ☐ *Reclutamiento → Reportes → Rendimiento por reclutador*: Mónica con presentados, aptos y contratados.
- ☐ *Resumen de reclutamiento* cuenta vacantes y candidatos; *Exportar Excel* descarga el archivo.
- ☐ Un usuario sin el permiso *Reportes de reclutamiento* no ve la sección.

---

## 2. Reclutador — el día a día

> **Como** reclutadora, **quiero** ver solo mis vacantes y mover a mis candidatos por las etapas, **para** cubrir las
> posiciones lo antes posible.

### HU-R1 · Activar mi cuenta
- ☐ Desde el correo de invitación defino mi contraseña y entro a la App Administración.
- ☐ Mi menú muestra solo *Mis vacantes* y *Mis candidatos*: **no** veo Clientes, Reportes, Usuarios ni Nómina.

### HU-R2 · Empezar el día con mis vacantes
**Como** reclutadora, **quiero** ver mis vacantes y qué hay pendiente, **para** organizar mi día.

- ☐ *Mis vacantes* lista solo las vacantes donde soy líder o apoyo.
- ☐ Cada una muestra estatus, solicitadas vs contratadas y días abierta; al abrirla, el Pipeline cuenta los candidatos
  de cada etapa.
- ☐ La campana 🔔 muestra avisos: «Te asignaron … como líder/apoyo» y los documentos subidos.
- ☐ No puedo crear ni editar vacantes (solo comentar).

### HU-R3 · Capturar un candidato nuevo
- ☐ Desde la vacante, *Nuevo candidato* con correo `+candidato2`, fuente (ej. Facebook) y sueldo ofrecido.
- ☐ El cliente, puesto y reclutador se heredan de la vacante (no los edito).
- ☐ El candidato recibe el correo con su enlace.

### HU-R4 · Mover candidatos en el Kanban
**Como** reclutadora, **quiero** arrastrar las tarjetas entre etapas, **para** reflejar el avance real.

- ☐ Arrastro de **Nuevo → Contactado → Entrevista**.
- ☐ Mover a **No apto** o **Descartado** me pide un motivo.
- ☐ La pestaña *Historial* del candidato muestra cada cambio de etapa con fecha y autor.

### HU-R5 · Programar una entrevista y escribir notas
- ☐ Perfil del candidato → *Entrevistas* → programo fecha y entrevistador. La lista de entrevistadores es solo el
  equipo de reclutamiento.
- ☐ En **Notas** escribo una nota con título y lista (H1–H3, negritas, listas, enlace).
- ☐ La nota aparece arriba, con mi nombre y fecha. Es interna: ni el cliente ni el candidato la ven.

### HU-R6 · Revisar y validar documentos
**Como** reclutadora, **quiero** revisar los documentos que sube el candidato, **para** completar su expediente.

- ☐ La campana avisa «… subió Identificación oficial».
- ☐ Pestaña *Documentos*: veo el checklist con % de avance; el documento está *Entregado*.
- ☐ **Validar** → *Validado*. **Rechazar** con motivo → el candidato ve el motivo y puede volver a subirlo.
- ☐ El perfil muestra los datos que capturó el candidato (CURP, RFC, domicilio, fecha de nacimiento).

### HU-R7 · Responder al cliente
- ☐ En la **vacante**, pestaña *Comentarios del cliente* (no en el perfil del candidato): veo lo que escribió el cliente
  y le respondo.
- ☐ Mi respuesta aparece en el portal del cliente.

### HU-R8 · Presentar y contratar
- ☐ Muevo al candidato a **Apto** → el cliente ya ve su nombre completo.
- ☐ **Mover a Contratado**: si faltan documentos obligatorios, el diálogo dice «Faltan N documentos obligatorios por
  entregar» pero deja contratar. Con la fecha de ingreso → «1 de 2 cubiertas».
- ☐ Con el segundo contratado, la vacante queda *Cubierta*.
- ☐ El contratado **no** tiene botón para pasarlo a Nómina: su proceso termina aquí (es empleado del cliente).

---

## 3. Candidato

> **Como** candidato, **quiero** completar mis datos y subir mis documentos desde el celular, **para** avanzar en mi
> postulación sin ir a la oficina.

### HU-C1 · Entrar sin contraseña
- ☐ El correo «Tu postulación: …» me lleva a la app: escribo mi correo, me llega un enlace y con él entro, sin
  contraseña.
- ☐ La primera vez acepto el **aviso de privacidad**: dice que mis datos se usan solo para el reclutamiento y que, si
  me contratan, trabajaré para la empresa de la vacante.
- ☐ Un enlace viejo o usado me deja pedir uno nuevo con mi correo.

### HU-C2 · Completar mi perfil
- ☐ *Mi perfil*: capturo CURP, RFC, NSS, celular y domicilio → Guardar.
- ☐ Una CURP con formato inválido muestra el error en español.
- ☐ No hay ninguna sección de cuenta bancaria ni menciones de nómina o pago.

### HU-C3 · Subir mis documentos
- ☐ *Mis documentos* muestra los que pide la vacante.
- ☐ Subo la INE (PDF) y el comprobante de domicilio (JPG; en el celular, con la cámara) → *Entregado*.
- ☐ Si el reclutador rechaza uno, veo *Rechazado* con el motivo y puedo subirlo otra vez.

### HU-C4 · Seguir mi postulación
- ☐ *Mi postulación* muestra la vacante, mi etapa en palabras sencillas y el contacto de mi reclutador.
- ☐ Al cambiar de etapa en la App Administración, veo la nueva etapa al actualizar.
- ☐ No veo notas internas, otros candidatos ni el sueldo de otros.
- ☐ Se ve bien en un celular: las tres pestañas caben sin cortarse.
- ☐ Al ser contratado veo «¡Felicidades! La empresa te contrató…» y mi fecha de ingreso.

---

## 4. Cliente

> **Como** contacto del cliente (HEB), **quiero** consultar el avance de mis vacantes, **para** saber cuándo tendré a la
> gente sin tener que llamar.

### HU-K1 · Entrar al portal
- ☐ El correo de invitación me lleva al portal: escribo mi correo, me llega un enlace y con él entro, sin contraseña.
- ☐ El enlace del cliente no abre la App Administración.

### HU-K2 · Ver mis vacantes
- ☐ *Mis vacantes* muestra solo las vacantes de HEB marcadas como visibles.
- ☐ Cada tarjeta: estatus, solicitadas vs contratadas, días abierta, reclutador líder (nombre y teléfono), candidatos
  por etapa.
- ☐ **No** veo sexo ni edad de la vacante.
- ☐ Una vacante sin «Visible en el portal del cliente» no aparece.

### HU-K3 · Ver candidatos con privacidad
**Como** cliente, **quiero** ver a los candidatos que me presentan, **para** prepararme para recibirlos, respetando su
privacidad hasta que estén aprobados.

- ☐ Antes de *Apto*: solo iniciales, fuente, etapa y fecha de captura.
- ☐ Desde *Apto*: nombre completo, sueldo ofrecido, fecha de entrevista y % de documentos completos.
- ☐ Nunca veo los documentos personales, las notas internas ni exportaciones.

### HU-K4 · Comentar con el reclutador
- ☐ En la vacante escribo un comentario → aparece en la conversación y en *Comentarios*.
- ☐ Veo la respuesta del reclutador (HU-R7).

### HU-K5 · Ver la vacante cubierta
- ☐ Tras las dos contrataciones, la vacante aparece *Cubierta* con 2 de 2.

---

## 5. Orden sugerido para la demo (≈ 30 min)

1. **Admin** HU-A1 → A3 → A4 → A5 → A6 → A7 (configuración en vivo, 10 min).
2. **Reclutador** HU-R1 → R2 → R3/A8 → R4 → R5 (5 min).
3. **Candidato** en el celular: HU-C1 → C2 → C3 → C4 (5 min).
4. **Reclutador** HU-R6 → R8 a *Apto*; **Cliente** HU-K1 → K3 (antes y después de *Apto*) → K4 → R7 (5 min).
5. **Cierre**: contratar → HU-K5 → HU-A10 reportes, y HU-A9 «Ver como reclutador» como extra (5 min).

## 6. Notas para la demo

- **Notas con título:** el botón H1/H2/H3 ya funciona en una nota vacía; si en DEV todavía no está la versión nueva,
  escriba el texto primero, selecciónelo y luego elija el título.
- **El tablero no impone orden:** se puede pasar de *Nuevo* a *Apto* directamente (decisión del dueño); úselo con
  intención en la demo.
- **Dos vacantes del mismo cliente, puesto y fecha** se llaman igual («… · 25/11/2026») en el portal del cliente; si
  duplica una vacante en la demo, cambie la fecha de solicitud o el puesto para distinguirlas.
- **Probado en el segundo ensayo local (2026-11-25):** verificación en dos pasos con código y con código de
  recuperación (HU-A1), aviso de CURP duplicada (HU-A8), vacante oculta en el portal (HU-K2) y enlace ya usado
  (HU-C1). **Sin probar:** la descarga de Excel (HU-A10); revísela usted en DEV.

> Si algo falla en el ensayo, anótelo con la historia (ej. «HU-R6, el rechazo no llega al candidato») y la hora, para
> revisar los registros de DEV.
