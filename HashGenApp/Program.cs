using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        bool result = BCrypt.Net.BCrypt.Verify("Password123!", "$2a$11$lmMNX5LQqFvqXfXBX5qYKuLFPluaNFWsDzRC6KkNxwOVg1LdVYgUa".Replace("$", "$"));
        Console.WriteLine("Verify Result: " + result);
    }
}
