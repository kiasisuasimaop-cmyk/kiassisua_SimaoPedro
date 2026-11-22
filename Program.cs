using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RegistoUtentes {
    enum Perfil { Medico = 1, Enfermeiro = 2, Administrativo = 3, Administrador = 4 }

    class Conta { public string Username, Hash; public Perfil Perfil; }

    class Consulta {
        public DateTime Data;
        public string Diagnostico = "", Notas = "", Tratamento = "", Medico = "";
    }

    class Utente {
        public string NumeroRegisto, Nome, Genero, Telefone, Morada;
        public string NIF = "", TipoSanguineo = "", Alergias = "", DoencasCronicas = "";
        public DateTime DataNascimento;
        public List<Consulta> Historico = new List<Consulta>();
        public int Idade { get {
            int i = DateTime.Today.Year - DataNascimento.Year;
            return DataNascimento.Date > DateTime.Today.AddYears(-i) ? i - 1 : i; } }
    }

    class MenuItem {
        public string Titulo; public Perfil[] Perfis; public Action Acao;
        public MenuItem(string t, Perfil[] p, Action a) { Titulo = t; Perfis = p; Acao = a; }
    }

    // Dados em memória + ficheiros de texto (utentes.txt, contas.txt, auditoria.txt)
    class Sistema {
        const string FU = "utentes.txt", FC = "contas.txt", FA = "auditoria.txt";
        public static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public List<Utente> Utentes = new List<Utente>();
        public List<Conta> Contas = new List<Conta>();

        public static string Hash(string s) {
            using (SHA256 h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "");
        }
        static bool Igual(string a, string b) { return a.Equals((b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }
        public Utente PorNumero(string s) { return Utentes.FirstOrDefault(u => Igual(u.NumeroRegisto, s)); }
        public Utente PorNIF(string s) { return Utentes.FirstOrDefault(u => u.NIF.Length > 0 && Igual(u.NIF, s)); }
        public Conta PorUser(string s) { return Contas.FirstOrDefault(c => Igual(c.Username, s)); }

        public string ProximoNumero() {
            int max = 0, n;
            foreach (Utente u in Utentes) if (int.TryParse(u.NumeroRegisto.Substring(3), out n) && n > max) max = n;
            return "UT-" + (max + 1).ToString("D4");
        }
        public List<Utente> Procurar(string termo) {
            termo = (termo ?? "").Trim();
            Utente u = PorNumero(termo) ?? PorNIF(termo);
            if (u != null) return new List<Utente> { u };
            return Utentes.Where(x => termo.Length > 0 && x.Nome.IndexOf(termo, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }
        public void Log(string user, string acao) {
            try { File.AppendAllText(FA, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " | " + user + " | " + acao + "\r\n", Encoding.UTF8); }
            catch { }
        }
        public void Guardar() {
            try {
                var l = new List<string>();
                foreach (Utente u in Utentes) {
                    l.Add(string.Join("|", "U", u.NumeroRegisto, u.Nome, u.DataNascimento.ToString("dd/MM/yyyy"), u.Genero,
                        u.Telefone, u.Morada, u.NIF, u.TipoSanguineo, u.Alergias, u.DoencasCronicas));
                    foreach (Consulta c in u.Historico) l.Add(string.Join("|", "C", u.NumeroRegisto,
                        c.Data.ToString("dd/MM/yyyy"), c.Diagnostico, c.Notas, c.Medico, c.Tratamento));
                }
                File.WriteAllLines(FU, l, Encoding.UTF8);
                File.WriteAllLines(FC, Contas.Select(c => c.Username + "|" + (int)c.Perfil + "|" + c.Hash), Encoding.UTF8);
            } catch (Exception ex) { Console.WriteLine("Aviso: não foi possível guardar (" + ex.Message + ")"); }
        }
        public void Carregar() {
            try {
                if (File.Exists(FU)) foreach (string linha in File.ReadAllLines(FU, Encoding.UTF8)) {
                    string[] p = linha.Split('|');
                    if (p[0] == "U" && p.Length >= 11)
                        Utentes.Add(new Utente { NumeroRegisto = p[1], Nome = p[2], Genero = p[4], Telefone = p[5],
                            Morada = p[6], NIF = p[7], TipoSanguineo = p[8], Alergias = p[9], DoencasCronicas = p[10],
                            DataNascimento = DateTime.ParseExact(p[3], "dd/MM/yyyy", IC) });
                    else if (p[0] == "C" && p.Length >= 7 && PorNumero(p[1]) != null)
                        PorNumero(p[1]).Historico.Add(new Consulta { Data = DateTime.ParseExact(p[2], "dd/MM/yyyy", IC),
                            Diagnostico = p[3], Notas = p[4], Medico = p[5], Tratamento = p[6] });
                }
                if (File.Exists(FC)) foreach (string linha in File.ReadAllLines(FC, Encoding.UTF8)) {
                    string[] p = linha.Split('|'); int perf;
                    if (p.Length >= 3 && int.TryParse(p[1], out perf))
                        Contas.Add(new Conta { Username = p[0], Perfil = (Perfil)perf, Hash = p[2] });
                }
            } catch (Exception ex) { Console.WriteLine("Aviso: erro ao carregar dados (" + ex.Message + ")"); }
            if (Contas.Count == 0) {   // contas iniciais
                string[][] d = { new[] { "admin", "4", "admin123" }, new[] { "medico", "1", "1234" },
                                 new[] { "enfermeiro", "2", "2345" }, new[] { "recepcao", "3", "3456" } };
                foreach (string[] x in d)
                    Contas.Add(new Conta { Username = x[0], Perfil = (Perfil)int.Parse(x[1]), Hash = Hash(x[2]) });
                Guardar();
            }
        }
    }

    class Program {
        static Sistema sis = new Sistema();
        static Conta sessao;

        static void Main() {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Sistema de Registo Eletrónico de Utentes";
            sis.Carregar();
            while (Login()) Sessao();
            sis.Guardar();
        }

        // ---------- apresentação ----------
        static void Cor(string t, ConsoleColor c) { Console.ForegroundColor = c; Console.WriteLine(t); Console.ResetColor(); }
        static void Sucesso(string m) { Cor(m, ConsoleColor.Green); }
        static void Erro(string m) { Cor(m, ConsoleColor.Red); }
        static void Cabecalho(string t) { Cor(new string('=', 60) + "\n  " + t + "\n" + new string('=', 60), ConsoleColor.Cyan); }
        static void Pausa() { Console.Write("\nPrima ENTER para continuar..."); Console.ReadLine(); }
        static void Feito(string acao, string msg) { sis.Guardar(); sis.Log(sessao.Username, acao); Console.WriteLine(msg); }

        // ---------- autenticação e menu dinâmico por perfil ----------
        static string LerSenha() {
            var sb = new StringBuilder(); ConsoleKeyInfo k;
            while ((k = Console.ReadKey(true)).Key != ConsoleKey.Enter) {
                if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); } }
                else if (!char.IsControl(k.KeyChar)) { sb.Append(k.KeyChar); Console.Write("*"); }
            }
            Console.WriteLine();
            return sb.ToString();
        }
        static bool Login() {
            Console.Clear();
            Cabecalho("SISTEMA DE REGISTO ELETRÓNICO DE UTENTES");
            Console.WriteLine("Ministério da Saúde\n(deixe o utilizador vazio para fechar o programa)\n");
            for (int t = 1; t <= 3; t++) {
                Console.Write("Utilizador: ");
                string user = (Console.ReadLine() ?? "").Trim();
                if (user.Length == 0) return false;
                Console.Write("Palavra-passe: ");
                string pass = LerSenha(); Conta c = sis.PorUser(user);
                if (c != null && c.Hash == Sistema.Hash(pass)) { sessao = c; sis.Log(c.Username, "Início de sessão"); return true; }
                Erro("Credenciais inválidas (" + t + "/3).\n");
            }
            Console.Write("Número máximo de tentativas excedido."); Console.ReadLine();
            return false;
        }
        static List<MenuItem> ConstruirMenu() {
            Perfil M = Perfil.Medico, E = Perfil.Enfermeiro, A = Perfil.Administrativo, D = Perfil.Administrador;
            var todos = new List<MenuItem> {
                new MenuItem("Registar novo utente", new[] { A, D, M }, RegistarUtente),
                new MenuItem("Editar dados de um utente", new[] { A, D, M }, EditarUtente),
                new MenuItem("Eliminar utente", new[] { A, D }, EliminarUtente),
                new MenuItem("Pesquisar utente (nº registo, NIF ou nome)", new[] { M, E, A, D }, PesquisarUtente),
                new MenuItem("Listar todos os utentes", new[] { M, E, A, D }, ListarUtentes),
                new MenuItem("Registar consulta", new[] { M }, RegistarConsulta),
                new MenuItem("Editar consulta", new[] { M }, () => GerirConsulta(false)),
                new MenuItem("Eliminar consulta", new[] { M }, () => GerirConsulta(true)),
                new MenuItem("Consultar histórico clínico", new[] { M, E }, ConsultarHistorico),
                new MenuItem("Exportar ficha do utente para ficheiro", new[] { M, E }, ExportarFicha),
                new MenuItem("Estatísticas", new[] { M, E, A, D }, Estatisticas),
                new MenuItem("Gerir utilizadores do sistema", new[] { D }, GerirUtilizadores),
                new MenuItem("Ver registo de auditoria", new[] { D }, VerAuditoria),
                new MenuItem("Alterar a minha palavra-passe", new[] { M, E, A, D }, AlterarMinhaSenha)
            };
            return todos.Where(i => i.Perfis.Contains(sessao.Perfil)).ToList();
        }
        static void Sessao() {
            var menu = ConstruirMenu();
            while (true) {
                Console.Clear(); Cabecalho("MENU PRINCIPAL");
                Console.WriteLine("Utilizador: " + sessao.Username + " | Perfil: " + sessao.Perfil);
                Console.WriteLine("Utentes registados: " + sis.Utentes.Count + "\n");
                for (int i = 0; i < menu.Count; i++) Console.WriteLine((i + 1) + " - " + menu[i].Titulo);
                Console.Write("0 - Terminar sessão\n\nOpção: ");
                int n;
                if (!int.TryParse((Console.ReadLine() ?? "").Trim(), out n) || n < 0 || n > menu.Count) Erro("Opção inválida.");
                else if (n == 0) { sis.Log(sessao.Username, "Fim de sessão"); sis.Guardar(); return; }
                else { Console.Clear(); Cabecalho(menu[n - 1].Titulo.ToUpper()); Console.WriteLine(); menu[n - 1].Acao(); }
                Pausa();
            }
        }

        // ---------- leitura e validação (atual != null: edição, ENTER mantém e "-" apaga) ----------
        static string Limpar(string s) { return (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim(); }
        static string Opc(string v, string d) { return v.Length == 0 ? d : v; }
        static bool Confirmar(string p) { Console.Write(p + " (S/N): "); return (Console.ReadLine() ?? "").Trim().ToUpper() == "S"; }

        static string LerTexto(string texto, string atual, bool obrig) {
            while (true) {
                Console.Write(texto + (string.IsNullOrEmpty(atual) ? "" : " [" + atual + "]") + ": ");
                string v = Limpar(Console.ReadLine());
                if (v == "-" && atual != null && !obrig) return "";
                if (v.Length > 0) return v;
                if (atual != null || !obrig) return atual ?? "";
                Console.WriteLine("Campo obrigatório.");
            }
        }
        static DateTime LerData(string texto, DateTime? atual) {
            while (true) {
                Console.Write(texto + (atual.HasValue ? " [" + atual.Value.ToString("dd/MM/yyyy") + "]" : " (dd/MM/aaaa)") + ": ");
                string s = (Console.ReadLine() ?? "").Trim(); DateTime d;
                if (s.Length == 0 && atual.HasValue) return atual.Value;
                if (!DateTime.TryParseExact(s, "dd/MM/yyyy", Sistema.IC, DateTimeStyles.None, out d))
                    Console.WriteLine("Formato inválido. Use dd/MM/aaaa.");
                else if (d.Date > DateTime.Today || d.Year < 1900) Console.WriteLine("Data inválida (futura ou < 1900).");
                else return d;
            }
        }
        static string LerLista(string texto, string atual, string[] validos, bool obrig) {
            while (true) {
                Console.Write(texto + " (" + string.Join("/", validos) + ")" + (string.IsNullOrEmpty(atual) ? "" : " [" + atual + "]") + ": ");
                string v = (Console.ReadLine() ?? "").Trim().ToUpper();
                if (v.Length == 0 && !string.IsNullOrEmpty(atual)) return atual;
                if ((v.Length == 0 || v == "-") && !obrig) return "";
                if (validos.Contains(v)) return v;
                Console.WriteLine("Valor inválido.");
            }
        }
        static string LerNIF(Utente atual) {
            while (true) {
                Console.Write("NIF" + (atual == null ? " (ENTER se não aplicável)" : " [" + Opc(atual.NIF, "nenhum") + "] ('-' remove)") + ": ");
                string n = (Console.ReadLine() ?? "").Trim();
                if (n.Length == 0) return atual == null ? "" : atual.NIF;
                if (n == "-") return "";
                Utente o = sis.PorNIF(n);
                if (n.Length < 9 || n.Length > 14 || !n.All(char.IsLetterOrDigit)) Console.WriteLine("NIF inválido (9 a 14 caracteres).");
                else if (o != null && o != atual) Console.WriteLine("NIF já associado ao utente " + o.NumeroRegisto + ".");
                else return n.ToUpper();
            }
        }
        static string LerSenhaNova() {
            while (true) {
                Console.Write("Nova palavra-passe (mín. 4 caracteres): "); string a = LerSenha();
                if (a.Length < 4) { Console.WriteLine("Demasiado curta."); continue; }
                Console.Write("Confirmar palavra-passe: ");
                if (a == LerSenha()) return a;
                Console.WriteLine("As palavras-passe não coincidem.");
            }
        }
        static Perfil LerPerfil() {
            Console.WriteLine("Perfil: 1-Médico  2-Enfermeiro  3-Administrativo  4-Administrador");
            return (Perfil)int.Parse(LerLista("Escolha", null, new[] { "1", "2", "3", "4" }, true));
        }
        static T Escolher<T>(List<T> l, Func<T, string> txt) where T : class {
            if (l.Count == 1) return l[0];
            for (int i = 0; i < l.Count; i++) Console.WriteLine((i + 1) + " - " + txt(l[i]));
            Console.Write("Escolha o número (0 cancela): ");
            int n;
            if (int.TryParse(Console.ReadLine(), out n) && n >= 1 && n <= l.Count) return l[n - 1];
            Console.WriteLine("Cancelado."); return null;
        }
        static Utente EscolherUtente() {
            Console.Write("Nº de registo, NIF ou parte do nome: ");
            var r = sis.Procurar(Console.ReadLine());
            if (r.Count == 0) { Console.WriteLine("Nenhum utente encontrado."); return null; }
            if (r.Count > 1) Console.WriteLine("\nForam encontrados " + r.Count + " utentes:");
            return Escolher(r, u => u.NumeroRegisto + " | " + u.Nome + " | " + u.DataNascimento.ToString("dd/MM/yyyy"));
        }
        static Consulta EscolherConsulta(Utente u) {
            if (u.Historico.Count == 0) { Console.WriteLine("Este utente não tem consultas."); return null; }
            Console.WriteLine("\nConsultas de " + u.Nome + ":");
            return Escolher(u.Historico.OrderByDescending(c => c.Data).ToList(), c => c.Data.ToString("dd/MM/yyyy") + " | " + c.Diagnostico);
        }

        // ---------- utentes ----------
        static void Ficha(Utente u, TextWriter w, bool hist) {
            foreach (string l in new[] { "Nº de registo : " + u.NumeroRegisto, "Nome          : " + u.Nome,
                "Nascimento    : " + u.DataNascimento.ToString("dd/MM/yyyy") + " (" + u.Idade + " anos)",
                "Género        : " + u.Genero, "Telefone      : " + u.Telefone, "Morada        : " + u.Morada,
                "NIF           : " + Opc(u.NIF, "(não indicado)"), "Tipo sanguíneo: " + Opc(u.TipoSanguineo, "(desconhecido)"),
                "Alergias      : " + Opc(u.Alergias, "(nenhuma registada)"),
                "Doenças crón. : " + Opc(u.DoencasCronicas, "(nenhuma registada)"), "Consultas     : " + u.Historico.Count })
                w.WriteLine(l);
            if (!hist) return;
            w.WriteLine(new string('=', 50));
            if (u.Historico.Count == 0) w.WriteLine("Sem consultas registadas.");
            int i = 1;
            foreach (Consulta c in u.Historico.OrderByDescending(x => x.Data))
                foreach (string m in new[] { "Consulta " + i++, "  Data        : " + c.Data.ToString("dd/MM/yyyy"),
                    "  Diagnóstico : " + c.Diagnostico, "  Notas       : " + Opc(c.Notas, "-"),
                    "  Tratamento  : " + Opc(c.Tratamento, "-"), "  Médico      : " + c.Medico, new string('-', 50) })
                    w.WriteLine(m);
        }
        static void FormUtente(Utente u, bool novo) {
            Func<string, string> A = v => novo ? null : v;
            u.Nome = LerTexto("Nome completo", A(u.Nome), true);
            u.DataNascimento = LerData("Data de nascimento", novo ? (DateTime?)null : u.DataNascimento);
            u.Genero = LerLista("Género", novo ? null : u.Genero.Substring(0, 1), new[] { "M", "F" }, true) == "M" ? "Masculino" : "Feminino";
            u.Telefone = LerTexto("Telefone", A(u.Telefone), true);
            u.Morada = LerTexto("Morada", A(u.Morada), true);
            u.NIF = LerNIF(novo ? null : u);
            u.TipoSanguineo = LerLista("Tipo sanguíneo", A(u.TipoSanguineo), new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" }, false);
            u.Alergias = LerTexto("Alergias", A(u.Alergias), false);
            u.DoencasCronicas = LerTexto("Doenças crónicas", A(u.DoencasCronicas), false);
        }
        static void RegistarUtente() {
            do {
                var u = new Utente();
                FormUtente(u, true);
                u.NumeroRegisto = sis.ProximoNumero(); sis.Utentes.Add(u);
                Feito("Registou utente " + u.NumeroRegisto, "");
                Sucesso("Utente registado! Nº de registo eletrónico: " + u.NumeroRegisto + "\n");
            } while (Confirmar("Registar outro utente?"));
        }
        static void EditarUtente() {
            Utente u = EscolherUtente(); if (u == null) return;
            Console.WriteLine("\n(ENTER mantém o valor atual; '-' apaga os campos opcionais)\n");
            FormUtente(u, false);
            Feito("Editou utente " + u.NumeroRegisto, "\nDados atualizados.");
        }
        static void EliminarUtente() {
            Utente u = EscolherUtente(); if (u == null) return;
            Console.WriteLine(); Ficha(u, Console.Out, false);
            if (!Confirmar("\nEliminar este utente e todo o seu histórico clínico?")) { Console.WriteLine("Cancelado."); return; }
            sis.Utentes.Remove(u);
            Feito("Eliminou utente " + u.NumeroRegisto, "Utente eliminado.");
        }
        static void PesquisarUtente() {
            Console.Write("Nº de registo, NIF ou parte do nome: ");
            var r = sis.Procurar(Console.ReadLine());
            Console.WriteLine(r.Count == 0 ? "Nenhum utente encontrado." : "\n" + r.Count + " resultado(s):\n");
            foreach (Utente u in r) { Ficha(u, Console.Out, false); Console.WriteLine(new string('-', 45)); }
        }
        static void ListarUtentes() {
            if (sis.Utentes.Count == 0) { Console.WriteLine("Nenhum utente registado."); return; }
            Console.Write("Ordenar por: 1-Nº registo  2-Nome  3-Idade\nOpção [1]: ");
            string o = (Console.ReadLine() ?? "").Trim();
            IEnumerable<Utente> l = o == "2" ? sis.Utentes.OrderBy(x => x.Nome) :
                o == "3" ? sis.Utentes.OrderBy(x => x.DataNascimento) : (IEnumerable<Utente>)sis.Utentes;
            Console.WriteLine("\n{0,-9} {1,-28} {2,-6} {3,-10} {4}\n" + new string('-', 66), "Nº", "Nome", "Idade", "Género", "Consultas");
            foreach (Utente u in l) Console.WriteLine("{0,-9} {1,-28} {2,-6} {3,-10} {4}", u.NumeroRegisto,
                u.Nome.Length > 27 ? u.Nome.Substring(0, 27) : u.Nome, u.Idade, u.Genero, u.Historico.Count);
            Console.WriteLine("\nTotal: " + sis.Utentes.Count);
        }

        // ---------- consultas e histórico clínico ----------
        static void FormConsulta(Consulta c, bool novo) {
            c.Data = LerData("Data do atendimento", novo ? (DateTime?)null : c.Data);
            c.Diagnostico = LerTexto("Diagnóstico", novo ? null : c.Diagnostico, true);
            c.Notas = LerTexto("Notas clínicas / observações", novo ? null : c.Notas, false);
            c.Tratamento = LerTexto("Tratamento / prescrição", novo ? null : c.Tratamento, false);
        }
        static void RegistarConsulta() {
            Utente u = EscolherUtente(); if (u == null) return;
            Console.WriteLine("\nUtente: " + u.NumeroRegisto + " - " + u.Nome + "\n");
            var c = new Consulta { Medico = sessao.Username };
            FormConsulta(c, true); u.Historico.Add(c);
            Feito("Registou consulta em " + u.NumeroRegisto, "");
            Sucesso("\nConsulta registada.");
        }
        static void GerirConsulta(bool eliminar) {
            Utente u = EscolherUtente(); if (u == null) return;
            Consulta c = EscolherConsulta(u); if (c == null) return;
            if (eliminar) {
                if (!Confirmar("Eliminar esta consulta?")) { Console.WriteLine("Cancelado."); return; }
                u.Historico.Remove(c);
            } else { Console.WriteLine("\n(ENTER mantém o valor atual)\n"); FormConsulta(c, false); }
            Feito((eliminar ? "Eliminou" : "Editou") + " consulta de " + u.NumeroRegisto,
                eliminar ? "Consulta eliminada." : "\nConsulta atualizada.");
        }
        static void ConsultarHistorico() {
            Utente u = EscolherUtente(); if (u == null) return;
            Console.WriteLine(); Ficha(u, Console.Out, true);
            sis.Log(sessao.Username, "Consultou histórico de " + u.NumeroRegisto);
        }
        static void ExportarFicha() {
            Utente u = EscolherUtente(); if (u == null) return;
            try {
                Directory.CreateDirectory("fichas");
                string f = Path.Combine("fichas", u.NumeroRegisto + ".txt");
                using (var w = new StreamWriter(f, false, Encoding.UTF8)) {
                    w.WriteLine("FICHA CLÍNICA - gerada em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")); w.WriteLine();
                    Ficha(u, w, true);
                }
                sis.Log(sessao.Username, "Exportou ficha de " + u.NumeroRegisto);
                Console.WriteLine("Ficha exportada para: " + Path.GetFullPath(f));
            } catch (Exception ex) { Erro("Erro ao exportar: " + ex.Message); }
        }
        static void Estatisticas() {
            var U = sis.Utentes;
            Console.WriteLine("Total de utentes   : " + U.Count);
            Console.WriteLine("  Masculino        : " + U.Count(x => x.Genero == "Masculino"));
            Console.WriteLine("  Feminino         : " + U.Count(x => x.Genero == "Feminino"));
            Console.WriteLine("Com NIF associado  : " + U.Count(x => x.NIF.Length > 0));
            Console.WriteLine("Total de consultas : " + U.Sum(x => x.Historico.Count));
            if (U.Count == 0) return;
            Console.WriteLine("\nFaixas etárias:\n  0-17 anos        : " + U.Count(x => x.Idade < 18) +
                "\n  18-59 anos       : " + U.Count(x => x.Idade >= 18 && x.Idade < 60) +
                "\n  60+ anos         : " + U.Count(x => x.Idade >= 60));
            var top = U.SelectMany(x => x.Historico).GroupBy(c => c.Diagnostico.ToLower())
                .OrderByDescending(g => g.Count()).Take(5).ToList();
            if (top.Count > 0) Console.WriteLine("\nDiagnósticos mais frequentes:");
            foreach (var g in top) Console.WriteLine("  " + g.Key + " : " + g.Count());
        }

        // ---------- administração: utilizadores e auditoria ----------
        static void GerirUtilizadores() {
            while (true) {
                Console.Clear(); Cabecalho("GESTÃO DE UTILIZADORES");
                foreach (Conta x in sis.Contas) Console.WriteLine("  " + x.Username.PadRight(18) + x.Perfil);
                Console.Write("\n1 - Criar utilizador\n2 - Alterar perfil\n3 - Repor palavra-passe\n" +
                    "4 - Eliminar utilizador\n0 - Voltar\n\nOpção: ");
                string op = (Console.ReadLine() ?? "").Trim();
                if (op == "0") return;
                Console.WriteLine();
                if (op == "1") {
                    string nome = LerTexto("Nome de utilizador", null, true).Replace(" ", "");
                    if (sis.PorUser(nome) != null) Console.WriteLine("Esse utilizador já existe.");
                    else {
                        var c = new Conta { Username = nome, Perfil = LerPerfil() };
                        c.Hash = Sistema.Hash(LerSenhaNova()); sis.Contas.Add(c);
                        Feito("Criou utilizador " + nome, "Utilizador criado.");
                    }
                } else if (op.Length == 1 && "234".Contains(op)) {
                    Conta c = sis.PorUser(LerTexto("Utilizador", null, true));
                    bool unico = c != null && c.Perfil == Perfil.Administrador &&
                        sis.Contas.Count(x => x.Perfil == Perfil.Administrador) == 1;
                    if (c == null) Console.WriteLine("Utilizador não encontrado.");
                    else if (op == "3") { c.Hash = Sistema.Hash(LerSenhaNova()); Feito("Repôs palavra-passe de " + c.Username, "Palavra-passe alterada."); }
                    else if (unico || (op == "4" && c == sessao)) Console.WriteLine("Não permitido (único administrador ou conta própria).");
                    else if (op == "2") { c.Perfil = LerPerfil(); Feito("Alterou perfil de " + c.Username, "Perfil alterado."); }
                    else if (Confirmar("Eliminar o utilizador " + c.Username + "?")) {
                        sis.Contas.Remove(c); Feito("Eliminou utilizador " + c.Username, "Utilizador eliminado.");
                    }
                } else Console.WriteLine("Opção inválida.");
                Pausa();
            }
        }
        static void AlterarMinhaSenha() {
            Console.Write("Palavra-passe atual: ");
            if (sessao.Hash != Sistema.Hash(LerSenha())) { Erro("Palavra-passe incorreta."); return; }
            sessao.Hash = Sistema.Hash(LerSenhaNova());
            Feito("Alterou a própria palavra-passe", "Palavra-passe alterada.");
        }
        static void VerAuditoria() {
            if (!File.Exists("auditoria.txt")) { Console.WriteLine("Sem registos."); return; }
            string[] l = File.ReadAllLines("auditoria.txt", Encoding.UTF8);
            Console.WriteLine("Últimos " + Math.Min(30, l.Length) + " registos:\n");
            foreach (string s in l.Skip(Math.Max(0, l.Length - 30))) Console.WriteLine(s);
        }
    }
}
