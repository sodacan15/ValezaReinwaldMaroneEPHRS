using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EPHRS
{
    // ---------- Data model (10 columns) ----------
    class Employee
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Department { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public decimal MonthlySalary { get; set; }
        public double TaxRate { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }
        public string Email { get; set; } = "";
    }

    // A validator parses once and hands back the parsed value
    delegate bool Validator(string input, out object value);

    // One editable column: label, validator, how to read it, how to write it
    class Field
    {
        public string Label { get; }
        public Validator Validate { get; }
        public Func<Employee, object> Get { get; }
        public Action<Employee, object> Set { get; }

        public Field(string label, Validator validate, Func<Employee, object> get, Action<Employee, object> set)
        {
            Label = label;
            Validate = validate;
            Get = get;
            Set = set;
        }
    }

    enum MessageType { Success, Error, Warning, Info }

    // ---------- Validators ----------
    static class FormatChecker
    {
        public static bool TryInt(string input, out object value)
        {
            bool ok = int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result);
            value = result;
            return ok;
        }

        public static bool TryDecimal(string input, out object value)
        {
            bool ok = decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result)
                      && result > 0;
            value = result;
            return ok;
        }

        public static bool TryTaxRate(string input, out object value)
        {
            bool ok = double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                      && result >= 0 && result <= 1;
            value = result;
            return ok;
        }

        public static bool TryDate(string input, out object value)
        {
            bool ok = DateTime.TryParseExact(input, "MM/dd/yyyy", CultureInfo.InvariantCulture,
                                             DateTimeStyles.None, out DateTime result);
            value = result;
            return ok;
        }

        public static bool TryYesNo(string input, out object value)
        {
            string t = input.Trim().ToUpper();
            value = t == "Y";
            return t == "Y" || t == "N";
        }

        public static bool TryEmail(string input, out object value)
        {
            string t = input.Trim();
            int at = t.IndexOf('@');
            bool ok = at > 0
                      && !t.Contains(' ')
                      && t.IndexOf('@', at + 1) == -1
                      && t.IndexOf('.', at) > at + 1
                      && !t.EndsWith(".");
            value = t;
            return ok;
        }

        public static bool TryText(string input, out object value)
        {
            value = input.Trim();
            return !string.IsNullOrWhiteSpace(input);
        }
    }

    class Program
    {
        const string AppTitle = "EMPLOYEE PAYROLL & HR MANAGEMENT SYSTEM";

        static List<Employee> employees = new List<Employee>();
        static int nextId = 1001;

        // Defined once, in column order, Id excluded (auto-assigned, not editable)
        static readonly Field[] Fields =
        {
            new Field("First name",     FormatChecker.TryText,    e => e.FirstName,     (e, v) => e.FirstName = (string)v),
            new Field("Last name",      FormatChecker.TryText,    e => e.LastName,      (e, v) => e.LastName = (string)v),
            new Field("Department",     FormatChecker.TryText,    e => e.Department,    (e, v) => e.Department = (string)v),
            new Field("Job title",      FormatChecker.TryText,    e => e.JobTitle,      (e, v) => e.JobTitle = (string)v),
            new Field("Monthly salary", FormatChecker.TryDecimal, e => e.MonthlySalary, (e, v) => e.MonthlySalary = (decimal)v),
            new Field("Tax rate (e.g. 0.12)", FormatChecker.TryTaxRate, e => e.TaxRate, (e, v) => e.TaxRate = (double)v),
            new Field("Hire date (MM/DD/YYYY)", FormatChecker.TryDate, e => e.HireDate, (e, v) => e.HireDate = (DateTime)v),
            new Field("Active? (Y/N)",  FormatChecker.TryYesNo,   e => e.IsActive,      (e, v) => e.IsActive = (bool)v),
            new Field("Email",          FormatChecker.TryEmail,   e => e.Email,         (e, v) => e.Email = (string)v),
        };

        // Table column widths: Id, First, Last, Dept, Job, Salary, Tax, Hire, Active, Email
        static readonly int[] Widths = { 5, 10, 10, 11, 16, 12, 5, 10, 8, 24 };
        static readonly string[] Headers =
            { "Id", "FirstName", "LastName", "Department", "JobTitle", "Salary", "Tax", "HireDate", "Status", "Email" };

        static void Main()
        {
            FitWindowToTable();
            ShowWelcome();

            bool running = true;
            while (running)
            {
                Console.Clear();
                Console.WriteLine(AppTitle);
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("1. Add Employee");
                Console.WriteLine("2. View Employees");
                Console.WriteLine("3. Search / Filter Employee");
                Console.WriteLine("4. Update Employee");
                Console.WriteLine("5. Delete Employee");
                Console.WriteLine("6. Exit");
                Console.Write("Choose an option: ");

                switch (Console.ReadLine()?.Trim())
                {
                    case "1": AddEmployee(); break;
                    case "2": ViewEmployees(); break;
                    case "3": SearchEmployees(); break;
                    case "4": UpdateEmployee(); break;
                    case "5": DeleteEmployee(); break;
                    case "6": running = false; break;
                    default:
                        ShowMessage("Invalid option. Choose 1 to 6.", MessageType.Error);
                        Pause();
                        break;
                }
            }

            ShowGoodbye();
        }

        // Widens the console window so the table does not wrap (Windows only, ignored if not allowed)
        static void FitWindowToTable()
        {
            try
            {
                int needed = BorderLine().Length + 1;
                if (OperatingSystem.IsWindows() && Console.WindowWidth < needed)
                {
                    Console.SetBufferSize(needed, Math.Max(Console.BufferHeight, Console.WindowHeight));
                    Console.SetWindowSize(Math.Min(needed, Console.LargestWindowWidth), Console.WindowHeight);
                }
            }
            catch
            {
                // Some terminals do not allow resizing, the table may wrap there
            }
        }

        // ---------- Screens ----------

        static void ShowWelcome()
        {
            Console.Clear();
            string line = new string('=', AppTitle.Length + 6);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(line);
            Console.WriteLine("   " + AppTitle + "   ");
            Console.WriteLine(line);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Welcome! Manage employee records and payroll details from one place.");
            Console.WriteLine();
            Console.WriteLine("Press any key to start...");
            Console.ReadKey(true);
        }

        static void ShowGoodbye()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Thank you for using the Employee Payroll & HR Management System.");
            Console.WriteLine("Goodbye!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Press any key to close...");
            Console.ReadKey(true);
        }

        // ---------- Helpers ----------

        static void ShowMessage(string text, MessageType type)
        {
            (ConsoleColor color, string tag) = type switch
            {
                MessageType.Success => (ConsoleColor.Green, "[SUCCESS]"),
                MessageType.Error => (ConsoleColor.Red, "[ERROR]"),
                MessageType.Warning => (ConsoleColor.Yellow, "[WARNING]"),
                _ => (ConsoleColor.Cyan, "[INFO]")
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"{tag} {text}");
            Console.ResetColor();
        }

        // Returns the parsed value, or null if allowSkip is true and the input was empty
        static object? PromptUntilValid(string label, Validator validator, bool allowSkip,
                                        ConsoleColor? labelColor = null)
        {
            while (true)
            {
                if (labelColor.HasValue) Console.ForegroundColor = labelColor.Value;
                Console.Write(label + ": ");
                Console.ResetColor();
                string input = Console.ReadLine() ?? "";

                if (allowSkip && input == "")
                    return null;

                if (validator(input, out object value))
                    return value;

                ShowMessage("Invalid input, try again.", MessageType.Error);
            }
        }

        static Employee? FindById(int id)
        {
            return employees.FirstOrDefault(e => e.Id == id);
        }

        static string Display(object value)
        {
            return value switch
            {
                bool b => b ? "Y" : "N",
                DateTime d => d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                decimal m => m.ToString("N2", CultureInfo.InvariantCulture),
                double t => t.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
        }

        // Cuts long text and ends it with "..." so rows never break
        static string Truncate(string text, int width)
        {
            if (text.Length <= width) return text;
            return text.Substring(0, width - 3) + "...";
        }

        static string BorderLine()
        {
            return "+" + string.Join("+", Widths.Select(w => new string('-', w + 2))) + "+";
        }

        static void PrintRow(string[] cells, bool isData, bool isActive)
        {
            Console.Write("|");
            for (int i = 0; i < cells.Length; i++)
            {
                string text = Truncate(cells[i], Widths[i]);
                bool rightAlign = i == 5 || i == 6; // salary and tax rate
                string padded = rightAlign ? text.PadLeft(Widths[i]) : text.PadRight(Widths[i]);

                Console.Write(" ");
                if (isData && i == 8)
                    Console.ForegroundColor = isActive ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write(padded);
                Console.ResetColor();
                Console.Write(" |");
            }
            Console.WriteLine();
        }

        static void PrintTable(List<Employee> list, string footerLabel)
        {
            Console.WriteLine(BorderLine());
            PrintRow(Headers, false, false);
            Console.WriteLine(BorderLine());

            foreach (Employee e in list)
            {
                PrintRow(new[]
                {
                    e.Id.ToString(), e.FirstName, e.LastName, e.Department, e.JobTitle,
                    Display(e.MonthlySalary), Display(e.TaxRate), Display(e.HireDate),
                    e.IsActive ? "Active" : "Inactive", e.Email
                }, true, e.IsActive);
            }

            Console.WriteLine(BorderLine());
            Console.WriteLine($"{footerLabel}: {list.Count} employee(s)");
        }

        static void ShowAllEmployees()
        {
            if (!employees.Any())
            {
                ShowMessage("No employees yet.", MessageType.Info);
                return;
            }
            PrintTable(employees.OrderBy(e => e.Id).ToList(), "Total");
        }

        static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to go back to the menu...");
            Console.ReadKey(true);
        }

        // ---------- Operations ----------

        static void AddEmployee()
        {
            Console.Clear();
            Console.WriteLine("ADD EMPLOYEE");
            Console.WriteLine();

            Employee a = new Employee { Id = nextId++ };

            foreach (Field f in Fields)
            {
                object v = PromptUntilValid(f.Label, f.Validate, allowSkip: false)!;
                f.Set(a, v);
            }

            employees.Add(a);
            Console.WriteLine();
            ShowMessage($"Employee added with Id {a.Id}.", MessageType.Success);
            Pause();
        }

        static void ViewEmployees()
        {
            Console.Clear();
            Console.WriteLine("VIEW EMPLOYEES");
            Console.WriteLine();
            ShowAllEmployees();
            Pause();
        }

        static void SearchEmployees()
        {
            Console.Clear();
            Console.WriteLine("SEARCH / FILTER EMPLOYEES");
            Console.WriteLine();

            if (!employees.Any())
            {
                ShowMessage("No employees yet.", MessageType.Info);
                Pause();
                return;
            }

            string keyword = (string)PromptUntilValid("Keyword (name, department, job title, or email)",
                                                      FormatChecker.TryText, allowSkip: false)!;

            List<Employee> results = employees.Where(e =>
                e.FirstName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.LastName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.Department.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.JobTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            Console.WriteLine();
            if (!results.Any())
                ShowMessage($"No matches found for \"{keyword}\".", MessageType.Info);
            else
                PrintTable(results, "Matches");

            Pause();
        }

        static void UpdateEmployee()
        {
            Console.Clear();
            Console.WriteLine("UPDATE EMPLOYEE");
            Console.WriteLine();

            if (!employees.Any())
            {
                ShowMessage("No employees yet.", MessageType.Info);
                Pause();
                return;
            }

            ShowAllEmployees();
            Console.WriteLine();

            int id = (int)PromptUntilValid("Employee Id to update", FormatChecker.TryInt, allowSkip: false)!;

            Employee? target = FindById(id);
            if (target == null)
            {
                ShowMessage($"Employee {id} was not found.", MessageType.Error);
                Pause();
                return;
            }

            Console.WriteLine();
            ShowMessage("Press Enter to keep the current value.", MessageType.Info);

            foreach (Field f in Fields)
            {
                string label = $"{f.Label} [{Display(f.Get(target))}]";
                object? v = PromptUntilValid(label, f.Validate, allowSkip: true);
                if (v != null)
                    f.Set(target, v);
            }

            Console.WriteLine();
            ShowMessage($"Employee {target.Id} updated.", MessageType.Success);
            Pause();
        }

        static void DeleteEmployee()
        {
            Console.Clear();
            Console.WriteLine("DELETE EMPLOYEE");
            Console.WriteLine();

            if (!employees.Any())
            {
                ShowMessage("No employees yet.", MessageType.Info);
                Pause();
                return;
            }

            ShowAllEmployees();
            Console.WriteLine();

            int id = (int)PromptUntilValid("Employee Id to delete", FormatChecker.TryInt, allowSkip: false)!;

            Employee? target = FindById(id);
            if (target == null)
            {
                ShowMessage($"Employee {id} was not found.", MessageType.Error);
                Pause();
                return;
            }

            Console.WriteLine();
            PrintTable(new List<Employee> { target }, "Selected");
            Console.WriteLine();

            bool confirm = (bool)PromptUntilValid(
                $"Are you sure you want to delete {target.FirstName} {target.LastName}? (Y/N)",
                FormatChecker.TryYesNo, allowSkip: false, labelColor: ConsoleColor.Yellow)!;

            if (!confirm)
            {
                ShowMessage("Delete cancelled. No changes were made.", MessageType.Warning);
                Pause();
                return;
            }

            employees.Remove(target);
            // nextId is not touched, so Ids are never reused
            ShowMessage($"Employee {target.Id} deleted.", MessageType.Success);
            Pause();
        }
    }
}
