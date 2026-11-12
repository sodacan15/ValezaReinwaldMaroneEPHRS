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

        static void Main()
        {
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
                        Console.WriteLine("Invalid option.");
                        Pause();
                        break;
                }
            }
        }

        // ---------- Helpers ----------

        // Returns the parsed value, or null if allowSkip is true and the input was empty
        static object? PromptUntilValid(string label, Validator validator, bool allowSkip)
        {
            while (true)
            {
                Console.Write(label + ": ");
                string input = Console.ReadLine() ?? "";

                if (allowSkip && input == "")
                    return null;

                if (validator(input, out object value))
                    return value;

                Console.WriteLine("Invalid input, try again.");
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

        static void PrintTable(List<Employee> list)
        {
            string row = "{0,-6}{1,-12}{2,-12}{3,-12}{4,-18}{5,14}  {6,-8}{7,-12}{8,-8}{9}";
            Console.WriteLine(row, "Id", "FirstName", "LastName", "Department", "JobTitle",
                              "MonthlySalary", "TaxRate", "HireDate", "Active", "Email");
            Console.WriteLine(new string('-', 120));

            foreach (Employee e in list)
            {
                Console.WriteLine(row, e.Id, e.FirstName, e.LastName, e.Department, e.JobTitle,
                                  Display(e.MonthlySalary), Display(e.TaxRate),
                                  Display(e.HireDate), e.IsActive ? "Active" : "Inactive", e.Email);
            }
        }

        static void ShowAllEmployees()
        {
            if (!employees.Any())
            {
                Console.WriteLine("No Employees.");
                return;
            }
            PrintTable(employees.OrderBy(e => e.Id).ToList());
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
            Console.WriteLine($"Employee added with Id {a.Id}.");
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
                Console.WriteLine("No Employees.");
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
                Console.WriteLine("No Matches.");
            else
                PrintTable(results);

            Pause();
        }

        static void UpdateEmployee()
        {
            Console.Clear();
            Console.WriteLine("UPDATE EMPLOYEE");
            Console.WriteLine();

            if (!employees.Any())
            {
                Console.WriteLine("No Employees.");
                Pause();
                return;
            }

            ShowAllEmployees();
            Console.WriteLine();

            int id = (int)PromptUntilValid("Employee Id to update", FormatChecker.TryInt, allowSkip: false)!;

            Employee? target = FindById(id);
            if (target == null)
            {
                Console.WriteLine("Employee Not Found.");
                Pause();
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press Enter to keep the current value.");

            foreach (Field f in Fields)
            {
                string label = $"{f.Label} [{Display(f.Get(target))}]";
                object? v = PromptUntilValid(label, f.Validate, allowSkip: true);
                if (v != null)
                    f.Set(target, v);
            }

            Console.WriteLine();
            Console.WriteLine("Employee Updated.");
            Pause();
        }

        static void DeleteEmployee()
        {
            Console.Clear();
            Console.WriteLine("DELETE EMPLOYEE");
            Console.WriteLine();

            if (!employees.Any())
            {
                Console.WriteLine("No Employees.");
                Pause();
                return;
            }

            ShowAllEmployees();
            Console.WriteLine();

            int id = (int)PromptUntilValid("Employee Id to delete", FormatChecker.TryInt, allowSkip: false)!;

            Employee? target = FindById(id);
            if (target == null)
            {
                Console.WriteLine("Employee Not Found.");
                Pause();
                return;
            }

            Console.WriteLine();
            PrintTable(new List<Employee> { target });
            Console.WriteLine();

            bool confirm = (bool)PromptUntilValid("Are you sure? (Y/N)", FormatChecker.TryYesNo, allowSkip: false)!;
            if (!confirm)
            {
                Console.WriteLine("Delete Cancelled.");
                Pause();
                return;
            }

            employees.Remove(target);
            // nextId is not touched, so Ids are never reused
            Console.WriteLine("Employee Deleted.");
            Pause();
        }
    }
}