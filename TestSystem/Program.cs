// This line imports the System namespace.
// Without it, you would have to write System.Console and System.DateTime.
using System;

// This is the main entry point for the application.
Console.WriteLine("Testing the 'System' namespace...");

// DateTime is a class within the System namespace.
// If 'using System;' works, this line will compile.
DateTime now = DateTime.Now;

// Console is another class from the System namespace.
Console.WriteLine($"The current time is: {now}");