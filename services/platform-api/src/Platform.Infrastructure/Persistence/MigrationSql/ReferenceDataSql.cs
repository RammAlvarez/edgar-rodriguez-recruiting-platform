namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// Catalog rows every installation starts with (DATA_MODEL §3.8, §3.9, §3.11). Idempotent: run by the S3 migration
/// and again by the tests after each truncation. Rows edited later from Catálogos are never overwritten. The name rows use
/// a bare `ON CONFLICT DO NOTHING`: their unique index was a plain column and is `lower(name)` since D-021.
/// </summary>
public static class ReferenceDataSql
{
    public const string Up = """
        INSERT INTO platform.banks (name, spei_code, supports_registration, supports_massive_payment) VALUES
            ('Banamex', '002', NULL, false), ('BBVA', '012', NULL, false), ('Santander', '014', NULL, false),
            ('Banjército', '019', NULL, false), ('HSBC', '021', NULL, false), ('Bajío', '030', NULL, false),
            ('Inbursa', '036', NULL, false), ('Mifel', '042', NULL, false), ('Scotiabank', '044', NULL, false),
            ('Banregio', '058', 'banregio', false), ('Invex', '059', NULL, false), ('Bansí', '060', NULL, false),
            ('Afirme', '062', NULL, false), ('Banorte', '072', 'banorte', false), ('Banco Azteca', '127', NULL, true),
            ('BanCoppel', '137', NULL, false), ('ABC Capital', '138', NULL, false), ('Hey Banco', '167', NULL, false),
            ('STP', '646', NULL, false), ('Mercado Pago', '722', NULL, false), ('Spin by OXXO', '728', NULL, false)
        ON CONFLICT (spei_code) DO NOTHING;

        INSERT INTO platform.document_types (code, name_es, has_expiry, default_validity_days, sort_order) VALUES
            ('INE', 'Identificación oficial (INE/IFE)', true, NULL, 10),
            ('CURP', 'CURP', false, NULL, 20),
            ('RFC', 'Constancia de situación fiscal', false, NULL, 30),
            ('NSS', 'Número de Seguro Social (IMSS)', false, NULL, 40),
            ('ACTA_NAC', 'Acta de nacimiento', false, NULL, 50),
            ('DOMICILIO', 'Comprobante de domicilio', true, 90, 60),
            ('ESTUDIOS', 'Comprobante de estudios', false, NULL, 70),
            ('CUENTA_BANCO', 'Estado de cuenta o carátula CLABE', false, NULL, 80),
            ('ANTECEDENTES', 'Carta de antecedentes no penales', true, NULL, 90),
            ('RECOMENDACION', 'Cartas de recomendación', false, NULL, 100),
            ('CV', 'Currículum', false, NULL, 110),
            ('FOTO', 'Fotografía', false, NULL, 120),
            ('CONTRATO', 'Contrato firmado', false, NULL, 130)
        ON CONFLICT (code) DO NOTHING;

        INSERT INTO platform.document_templates (name_es, scope, is_default) VALUES
            ('Candidato — estándar', 'candidate', true),
            ('Empleado — expediente', 'employee', true)
        ON CONFLICT DO NOTHING;

        INSERT INTO platform.document_template_items (template_id, document_type_id, is_required)
        SELECT t.uid, d.uid, x.required
        FROM (VALUES
                ('Candidato — estándar', 'INE', true), ('Candidato — estándar', 'CURP', true),
                ('Candidato — estándar', 'RFC', true), ('Candidato — estándar', 'NSS', true),
                ('Candidato — estándar', 'ACTA_NAC', true), ('Candidato — estándar', 'DOMICILIO', true),
                ('Candidato — estándar', 'ESTUDIOS', false), ('Candidato — estándar', 'ANTECEDENTES', false),
                ('Candidato — estándar', 'RECOMENDACION', false), ('Candidato — estándar', 'CV', false),
                ('Candidato — estándar', 'FOTO', false),
                ('Empleado — expediente', 'INE', true), ('Empleado — expediente', 'CURP', true),
                ('Empleado — expediente', 'RFC', true), ('Empleado — expediente', 'NSS', true),
                ('Empleado — expediente', 'ACTA_NAC', true), ('Empleado — expediente', 'DOMICILIO', true),
                ('Empleado — expediente', 'CUENTA_BANCO', true), ('Empleado — expediente', 'CONTRATO', true),
                ('Empleado — expediente', 'ESTUDIOS', false), ('Empleado — expediente', 'ANTECEDENTES', false),
                ('Empleado — expediente', 'FOTO', false)
             ) AS x(template, code, required)
        JOIN platform.document_templates t ON t.name_es = x.template
        JOIN platform.document_types d ON d.code = x.code
        ON CONFLICT (template_id, document_type_id) DO NOTHING;

        INSERT INTO platform.zones (name_es, state)
        SELECT name, 'Nuevo León' FROM unnest(ARRAY[
            'Abasolo', 'Agualeguas', 'Allende', 'Anáhuac', 'Apodaca', 'Aramberri', 'Bustamante', 'Cadereyta Jiménez',
            'El Carmen', 'Cerralvo', 'China', 'Ciénega de Flores', 'Doctor Arroyo', 'Doctor Coss', 'Doctor González',
            'Galeana', 'García', 'General Bravo', 'General Escobedo', 'General Terán', 'General Treviño',
            'General Zaragoza', 'General Zuazua', 'Guadalupe', 'Hidalgo', 'Higueras', 'Hualahuises', 'Iturbide',
            'Juárez', 'Lampazos de Naranjo', 'Linares', 'Los Aldamas', 'Los Herreras', 'Los Ramones', 'Marín',
            'Melchor Ocampo', 'Mier y Noriega', 'Mina', 'Montemorelos', 'Monterrey', 'Parás', 'Pesquería', 'Rayones',
            'Sabinas Hidalgo', 'Salinas Victoria', 'San Nicolás de los Garza', 'San Pedro Garza García',
            'Santa Catarina', 'Santiago', 'Vallecillo', 'Villaldama']) AS name
        ON CONFLICT DO NOTHING;

        INSERT INTO platform.education_levels (name_es, sort_order) VALUES
            ('Primaria', 10), ('Secundaria', 20), ('Preparatoria', 30), ('Carrera técnica', 40),
            ('Licenciatura', 50), ('Maestría', 60)
        ON CONFLICT DO NOTHING;

        INSERT INTO platform.benefits (name_es, is_default) VALUES
            ('Prestaciones de ley', true), ('IMSS', true), ('Vales de despensa', false), ('Fondo de ahorro', false),
            ('Transporte', false), ('Uniformes', false), ('Seguro de vida', false)
        ON CONFLICT DO NOTHING;
        """;
}
