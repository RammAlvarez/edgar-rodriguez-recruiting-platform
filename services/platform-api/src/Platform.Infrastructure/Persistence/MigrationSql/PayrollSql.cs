namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// S7 (Personal): the pay concepts of DATA_MODEL §5.3 as reference data (codes and Excel columns = <c>PayConcepts</c> of the
/// engine, checked by a test), the no-overlap rule of status periods, and the FK of <c>candidates.employee_id</c> that waited
/// for this table (PLAN D72).
/// </summary>
public static class PayrollSql
{
    /// <summary>Reference data: also put back by the test database reset, like <see cref="ReferenceDataSql"/>.</summary>
    public const string PayConcepts = """
        INSERT INTO platform.pay_concepts (code, name_es, kind, calc, default_rate, default_amount, export_column, sort_order, is_system) VALUES
            ('SALARY',             'Sueldo',                         'earning',   'per_day_worked',            NULL, NULL,   'SUELDO', 0, true),
            ('SEVENTH_DAY',        'Séptimo día',                    'earning',   'per_day_worked',            NULL, NULL,   'SEPTIMO DIA', 1, true),
            ('SUNDAY_PREMIUM',     'Prima dominical',                'earning',   'percent_of_daily_salary',   0.25, NULL,   'PRIMA DOMINICAL', 2, true),
            ('PARTIAL_HOURS',      'Horas incompletas',              'earning',   'qty_times_hourly_rate',     NULL, NULL,   'MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS', 3, true),
            ('HOLIDAY_FIRST_HALF', 'Festivo primera mitad',          'earning',   'qty_times_hourly_rate_x3',  NULL, NULL,   'FESTIVO PRIMERA MITAD', 4, true),
            ('OVERTIME',           'Horas extra',                    'earning',   'qty_times_hourly_rate',     NULL, NULL,   'HORAS EXTRA', 5, true),
            ('RETROACTIVE',        'Retroactivo',                    'earning',   'manual',                    NULL, NULL,   'RETROACTIVO', 6, true),
            ('REST_DAY_WORKED',    'Descanso laborado',              'earning',   'fixed_amount',              NULL, 500,    'DESCANSO LABORADO', 7, true),
            ('OVERTIME_DOUBLE',    'Tiempo extra doble',             'earning',   'qty_times_hourly_rate_x2',  NULL, NULL,   'TIEMPO EXTRA DOBLE', 8, true),
            ('OVERTIME_TRIPLE',    'Tiempo extra triple',            'earning',   'qty_times_hourly_rate_x3',  NULL, NULL,   'TIEMPO EXTRA TRIPLE', 9, true),
            ('HOLIDAY_WORKED',     'Festivo laborado',               'earning',   'qty_times_daily_salary_x2', NULL, NULL,   'FESTIVO LABORADO', 10, true),
            ('BONUS_DAYS_WORKED',  'Bono días trabajados',           'earning',   'fixed_amount',              NULL, NULL,   'BONO DIAS TRABAJADOS', 11, true),
            ('BONUS_PUNCTUALITY',  'Bono puntualidad y asistencia',  'earning',   'per_day_worked',            7,    400,    'BONO PUNTUALIDAD Y ASISTENCIA', 12, true),
            ('BONUS_PRODUCTIVITY', 'Bono productividad',             'earning',   'per_day_worked',            7,    360,    'BONO PRODUCTIVIDAD', 13, true),
            ('DEDUCTION',          'Descuento',                      'deduction', 'manual',                    NULL, NULL,   'DESCUENTO', 14, true),
            ('LOAN_INSTALLMENT',   'Abono a préstamo',               'deduction', 'fixed_amount',              NULL, NULL,   'DESCUENTO', 15, true),
            ('MANUAL_ADJUSTMENT',  'Ajuste manual',                  'earning',   'manual',                    NULL, NULL,   NULL, 16, true);
        """;

    public const string Up = PayConcepts + """

        -- No two status periods of an employee overlap; a baja (no end) runs to infinity.
        ALTER TABLE platform.employee_status_periods ADD CONSTRAINT ex_employee_status_periods_overlap
            EXCLUDE USING gist (employee_id WITH =, daterange(start_date, end_date, '[]') WITH &&);

        -- EF does not see this FK (raw SQL), so «Convertir en empleado» may update the candidate before the employee row is
        -- inserted in the same SaveChanges: checked at commit.
        ALTER TABLE platform.candidates ADD CONSTRAINT fk_candidates_employees_employee_id
            FOREIGN KEY (employee_id) REFERENCES platform.employees (uid) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
        """;

    public const string Down = """
        ALTER TABLE platform.candidates DROP CONSTRAINT fk_candidates_employees_employee_id;
        ALTER TABLE platform.employee_status_periods DROP CONSTRAINT ex_employee_status_periods_overlap;
        DELETE FROM platform.pay_concepts;
        """;
}
