namespace Platform.Infrastructure.Persistence.MigrationSql;

/// <summary>
/// S10: the freeze of a paid period lets the payment columns of its lines change — the account snapshot written when a line
/// goes into a pending-payments file, and the state the bank's answer gives it — and nothing else (DATA_MODEL §5.10–5.12).
/// The bank employee numbers come from one sequence per bank (DATA_MODEL §9).
/// </summary>
public static class PaymentFilesSql
{
    private const string PaymentColumns =
        "'{bank_id,account_type,account_number_enc,account_last4,holder_name,payment_status}'::text[]";

    public const string Up = $"""
        CREATE OR REPLACE FUNCTION platform.reject_paid_period_change() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE period uuid;
        DECLARE moved_from uuid;
        BEGIN
            IF TG_TABLE_NAME = 'prepayroll_line_items' THEN
                SELECT payroll_period_id INTO period FROM platform.prepayroll_lines
                WHERE uid = COALESCE(NEW.prepayroll_line_id, OLD.prepayroll_line_id);
            ELSE
                period := COALESCE(NEW.payroll_period_id, OLD.payroll_period_id);
                -- A row is not moved out of a paid period either.
                IF TG_OP = 'UPDATE' THEN
                    moved_from := OLD.payroll_period_id;
                END IF;
            END IF;
            -- A paid line still records where and whether it was paid.
            IF TG_TABLE_NAME = 'prepayroll_lines' AND TG_OP = 'UPDATE'
                AND to_jsonb(OLD) - {PaymentColumns} = to_jsonb(NEW) - {PaymentColumns} THEN
                RETURN NEW;
            END IF;
            IF EXISTS (SELECT 1 FROM platform.payroll_periods WHERE uid IN (period, moved_from) AND status = 'paid') THEN
                RAISE EXCEPTION 'The period is paid; its prenómina cannot change (%).', TG_TABLE_NAME USING ERRCODE = 'P0001';
            END IF;
            RETURN COALESCE(NEW, OLD);
        END $$;
        CREATE SEQUENCE platform.banorte_employee_seq START 2000000001 MAXVALUE 2999999999;
        CREATE SEQUENCE platform.banregio_employee_seq START 3000000001 MAXVALUE 3999999999;
        """;

    /// <summary>Back to the S9 function (no payment columns), without the sequences.</summary>
    public static readonly string Down = $"""
        DROP SEQUENCE platform.banregio_employee_seq;
        DROP SEQUENCE platform.banorte_employee_seq;
        {PrepayrollSql.Up.Split("CREATE TRIGGER", 2)[0].Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal)}
        """;
}
