using System;

namespace ArmA2
{
    /// <summary>
    /// Provides functionality to call the extension.
    /// </summary>
    public interface IExtension
    {
        /// <summary>
        /// Executes an extension function.
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>A String containing the result of executing the function.</returns>
        string Call(string args);
    }
}
