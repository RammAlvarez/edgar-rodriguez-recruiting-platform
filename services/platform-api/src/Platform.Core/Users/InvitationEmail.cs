using System.Net;
using Platform.Core.Abstractions;

namespace Platform.Core.Users;

/// <summary>The invitation (SPEC §6, ARCHITECTURE §4.1 step 1): staff set a password; Cliente users sign in by email link.</summary>
public static class InvitationEmail
{
    public static EmailMessage Build(User user, string inviter, string link)
    {
        var role = user.Role switch
        {
            UserRole.Administrator => "Administrador",
            UserRole.Recruiter => "Reclutador",
            _ => "Cliente",
        };
        var (action, button, note) = user.Role == UserRole.Client
            ? ("Entra al portal de clientes con este correo; te enviaremos un enlace para iniciar sesión, sin contraseña.",
                "Abrir el portal", "")
            : ("Para entrar, define tu contraseña con el enlace siguiente.", "Definir contraseña",
                "El enlace es de un solo uso y vence en una hora; si vence, pide que te reenvíen la invitación.");
        var subject = "Tu acceso a la Plataforma de Reclutamiento y Prenómina";
        var text = $"""
            Hola {user.DisplayName}:

            {inviter} te dio acceso a la Plataforma de Reclutamiento y Prenómina como {role}.
            {action}

            {link}

            {note}
            """;
        static string e(string value) => WebUtility.HtmlEncode(value);
        var html = $"""
            <p>Hola {e(user.DisplayName)}:</p>
            <p>{e(inviter)} te dio acceso a la Plataforma de Reclutamiento y Prenómina como <strong>{role}</strong>.<br>{e(action)}</p>
            <p><a href="{e(link)}">{button}</a></p>
            <p style="color:#555">{e(note)}</p>
            """;
        return new EmailMessage(user.Email, subject, html, text);
    }
}
