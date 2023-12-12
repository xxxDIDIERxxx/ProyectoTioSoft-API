using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TioSoft.BLL.Servicios.Contrato;
using TioSoft.DAL.Repositorios.Contrato;
using TioSoft.DTO;
using TioSoft.Model;


using SendGrid.Helpers.Mail;
using SendGrid;

namespace TioSoft.BLL.Servicios
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IGenericRepository<Usuario> _usuarioRepositorio;
        private readonly IMapper _mapper;

        public UsuarioService(IGenericRepository<Usuario> usuarioRepositorio, IMapper mapper)
        {
            _usuarioRepositorio = usuarioRepositorio;
            _mapper = mapper;
        }


        public async Task<List<UsuarioDTO>> Lista()
        {
            try
            {
                var queryUsuario = await _usuarioRepositorio.Consultar();
                var listaUsuarios = queryUsuario.Include(rol => rol.IdRolNavigation).ToList();

                return _mapper.Map<List<UsuarioDTO>>(listaUsuarios);
            }
            catch
            {
                throw;
            }
        }

        public async Task<SesionDTO> ValidarCredenciales(string correo, string clave)
        {
            try
            {
                var queryUsuario = await _usuarioRepositorio.Consultar(u =>
                    u.Correo == correo &&
                    u.Clave == clave
                );

                if (queryUsuario.FirstOrDefault() == null)
                    throw new TaskCanceledException("El usuario no existe");

                Usuario devolverUsuario = queryUsuario.Include(rol => rol.IdRolNavigation).First();

                return _mapper.Map<SesionDTO>(devolverUsuario);
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> RecuperarContrasena(string correo)
        {
            try
            {
                if (string.IsNullOrEmpty(correo))
                {
                    throw new Exception("El correo no puede estar vacío.");
                }

                var usuario = await _usuarioRepositorio.Obtener(u => u.Correo == correo);

                if (usuario == null)
                {
                    return false;
                }

                string nuevaContraseña = GenerarContrasena();

                usuario.Clave = nuevaContraseña;
                await _usuarioRepositorio.Editar(usuario);

                await EnviarCorreo(correo, nuevaContraseña);

                return true;
            }
            catch
            {
                throw;
            }
        }


        private string GenerarContrasena()
        {
            const string caracteres = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            var random = new Random();
            var contraseña = new string(Enumerable.Repeat(caracteres, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return contraseña;
        }

        private async Task EnviarCorreo(string correoDestino, string contraseña)
        {
            var apiKey = "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"; // API Key de SendGrid
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("pachitoche2501259@gmail.com", "De");
            var to = new EmailAddress(correoDestino, "Para");
            const string subject = "Recuperación de Contraseña para tu Cuenta TioSoft";
            string body = $@"
                            <!DOCTYPE html>
                            <html>
                            <head>
                                <style>
                                    h1 {{ font-size: 24px; color: #333; }}
                                    h3 {{ font-size: 18px; color: #007bff; }}
                                    p {{ font-size: 16px; color: #666; }}
                                    strong {{ font-weight: bold; }}
                                </style>
                            </head>
                            <body>
                                <h1>Recuperación de contraseña</h1>
                                <p>Tu contraseña temporal es: <h3>{contraseña}</h3> Te recomendamos cambiarla cuando ingreses.</p>
                                <p><strong>Para acceder al aplicativo, ve al inicio de sesión e ingresa tu correo. Utiliza la contraseña que te hemos enviado.</strong></p>
                            </body>
                            </html>
                            ";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", body);
            var response = await client.SendEmailAsync(msg);
        }


        public async Task<UsuarioDTO> Crear(UsuarioDTO modelo)
        {
            try
            {
                // Verificar si ya existe un usuario con el mismo correo
                var usuarioExistente = await _usuarioRepositorio.Obtener(p => p.Correo == modelo.Correo);

                if (usuarioExistente != null)
                {
                    throw new Exception("Ya existe un usuario con el mismo correo.");
                }

                var usuarioCreado = await _usuarioRepositorio.Crear(_mapper.Map<Usuario>(modelo));

                if (usuarioCreado.IdUsuario == 0)
                    throw new TaskCanceledException("No se pudo crear");

                var query = await _usuarioRepositorio.Consultar(u => u.IdUsuario == usuarioCreado.IdUsuario);

                usuarioCreado = query.Include(rol => rol.IdRolNavigation).First();

                return _mapper.Map<UsuarioDTO>(usuarioCreado);
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> Editar(UsuarioDTO modelo)
        {
            try
            {
                var usuarioModelo = _mapper.Map<Usuario>(modelo);

                var usuarioEncontrado = await _usuarioRepositorio.Obtener(u => u.IdUsuario == usuarioModelo.IdUsuario);

                if (usuarioEncontrado == null)
                    throw new TaskCanceledException("El usuario no existe");

                // Verificar si existe otro producto con el mismo nombre pero diferente ID
                var usuarioExistente = await _usuarioRepositorio.Obtener(u =>
                    u.Correo == modelo.Correo && u.IdUsuario != modelo.IdUsuario
                );
                if (usuarioExistente != null)
                {
                    throw new TaskCanceledException("Ya existe otro usuario con ese correo");
                }

                usuarioEncontrado.NombreCompleto = usuarioModelo.NombreCompleto;
                usuarioEncontrado.Correo = usuarioModelo.Correo;
                usuarioEncontrado.IdRol = usuarioModelo.IdRol;
                usuarioEncontrado.Clave = usuarioModelo.Clave;
                usuarioEncontrado.EsActivo = usuarioModelo.EsActivo;

                bool respuesta = await _usuarioRepositorio.Editar(usuarioEncontrado);

                if (!respuesta)
                    throw new TaskCanceledException("No se pudo editar");

                return respuesta;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> Eliminar(int id)
        {
            try
            {
                var usuarioEcontrado = await _usuarioRepositorio.Obtener(u => u.IdUsuario == id);

                if (usuarioEcontrado == null)
                    throw new TaskCanceledException("El usuario no existe");

                bool respuesta = await _usuarioRepositorio.Eliminar(usuarioEcontrado);

                if (!respuesta)
                    throw new TaskCanceledException("No se pudo eliminar");

                return respuesta;
            }
            catch
            {
                throw;
            }
        }

    }
}
