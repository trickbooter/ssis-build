﻿//-----------------------------------------------------------------------
//   Copyright 2017 Roman Tumaykin
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace SsisBuild.Tests
{
    public class BuildArgumentsTests : IDisposable
    {
        private readonly MethodInfo _parseMethod;
        private readonly MethodInfo _validateMethod;
        private readonly string _workingFolder;
        private readonly string _oldWorkingFolder;
        
        public BuildArgumentsTests()
        {
            _workingFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workingFolder);
            _oldWorkingFolder = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _workingFolder;
            _parseMethod = typeof(BuildArguments).GetMethod("Parse", BindingFlags.NonPublic|BindingFlags.Static);
            _validateMethod = typeof(BuildArguments).GetMethod("Validate", BindingFlags.NonPublic|BindingFlags.Instance);
        }

        public void Dispose()
        {
            Environment.CurrentDirectory = _oldWorkingFolder;
            Directory.Delete(_workingFolder, true);
        }


        #region Resolve tests
        [Fact]
        public void Pass_Parse_ProjectPath()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            var res = _parseMethod.Invoke(null, new object[] { new string[] {}}) as BuildArguments;
            Assert.NotNull(res);
            Assert.Equal(res.ProjectPath, filePath);
        }

        [Fact]
        public void Pass_Parse_FullProjectPath()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            var res = _parseMethod.Invoke(null, new object[] {new[] {"test.dtproj"}}) as BuildArguments;
            Assert.NotNull(res);
            Assert.Equal(res.ProjectPath, filePath);
        }

        [Fact]
        public void Pass_Parse_AllProperties()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            var args = new[]
            {
                "test.dtproj",
                $"-{nameof(BuildArguments.Configuration)}",
                "abc",
                $"-{nameof(BuildArguments.ProtectionLevel)}",
                "EncryptAllWithPassword",
                $"-{nameof(BuildArguments.NewPassword)}",
                "123",
                $"-{nameof(BuildArguments.Password)}",
                "234",
                $"-{nameof(BuildArguments.OutputFolder)}",
                ".\\sss",
                $"-{nameof(BuildArguments.ReleaseNotes)}",
                ".\\aaa",
                "-Parameter:Project::Parameter1",
                "asdfg",
                "-Parameter:Package1::Parameter1",
                "qwerty"
            };
            var res = _parseMethod.Invoke(null, new object[] { args }) as BuildArguments;
            Assert.NotNull(res);
            Assert.Equal(res.Configuration, "abc");
            Assert.Equal(res.ProtectionLevel, "EncryptAllWithPassword");
            Assert.Equal(res.NewPassword, "123");
            Assert.Equal(res.Password, "234");
            Assert.Equal(res.OutputFolder, ".\\sss");
            Assert.Equal(res.ReleaseNotes, ".\\aaa");
            Assert.NotNull(res.Parameters);
            Assert.True(res.Parameters.Count == 2);
            Assert.Equal(res.Parameters["Project::Parameter1"], "asdfg");
            Assert.Equal(res.Parameters["Package1::Parameter1"], "qwerty");
        }


        [Fact]
        public void Pass_Parse_Configuration()
        {
            var args = new[] { $"-{nameof(BuildArguments.Configuration)}", "Development" };
            var res = _parseMethod.Invoke(null, new object[] { args }) as BuildArguments;
            Assert.NotNull(res);
            Assert.Equal(res.Configuration, "Development");
        }

        [Fact]
        public void Fail_Parse_NoArgumentValue()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            // need to set configuration value, otherwise the call will fail;
            var args = new[] { $"-{nameof(BuildArguments.Configuration)}", "Development", $"-{nameof(BuildArguments.Password)}" };

            var exception = Record.Exception(() => _parseMethod.Invoke(null, new object[] { args }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, $"No value provided for an argument [{nameof(BuildArguments.Password)}]: (no value)", StringComparer.InvariantCultureIgnoreCase);
        }

        [Theory]
        [InlineData("dkjdkjkdj")]
        [InlineData("password")]
        [InlineData("-jdhdjhdj")]
        public void Fail_Parse_InvalidToken(string token)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            // need to set configuration value, otherwise the call will fail;
            var args = new[] { $"-{nameof(BuildArguments.Configuration)}", "Development", token };

            var exception = Record.Exception(() => _parseMethod.Invoke(null, new object[] { args }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, $"Invalid token \"{token}\"");
        }
        #endregion // Resolve tests

        #region Validate tests
        [Fact]
        public void Pass_Validate_ProjectPath()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");
            var buildArguments = _parseMethod.Invoke(null, new object[] { new string[] { } }) as BuildArguments;

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.Null(exception);
        }

        [Fact]
        public void Fail_Validate_NullProjectPath()
        {
            var buildArguments = Helpers.CreateBuildArguments();

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] {}));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            // ensure that it is a missing file exception
            Assert.Equal(exception.Message, $"Unable find any project file in {Environment.CurrentDirectory}.");
        }

        [Fact]
        public void Fail_Validate_InvalidProjectPath()
        {
            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "abc.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<FileNotFoundException>(exception);
        }

        [Fact]
        public void Fail_Validate_NoConfiguration()
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, "Configuration is a required argument.");
        }

        [Theory]
        [InlineData("XYZ")]
        [InlineData("ServerStorage")]
        [InlineData("EncryptSensitiveWithUserKey")]
        public void Fail_Validate_InvalidProtectionLevel(string protectionLevel)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProtectionLevel), protectionLevel);

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Password), "123");

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, $"Invalid value for argument [ProtectionLevel]: {protectionLevel}");
        }

        [Theory]
        [InlineData("DontSaveSensitive")]
        [InlineData("EncryptAllWithPassword")]
        [InlineData("EncryptSensitiveWithPassword")]
        public void Pass_Validate_ValidProtectionLevel(string protectionLevel)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProtectionLevel), protectionLevel);

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Password), "123");

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData("DontSaveSensitive", "123", null)]
        [InlineData("DontSaveSensitive", null, null)]
        [InlineData("EncryptAllWithPassword", null, "123")]
        [InlineData("EncryptAllWithPassword", "234", "123")]
        [InlineData("EncryptAllWithPassword", "123", null)]
        [InlineData("EncryptSensitiveWithPassword", null, "123")]
        [InlineData("EncryptSensitiveWithPassword", "234", "123")]
        [InlineData("EncryptSensitiveWithPassword", "123", null)]
        public void Pass_Validate_ProtectionLevelPasswordCombination(string protectionLevel, string password, string newPassword)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProtectionLevel), protectionLevel);

            if (password != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Password), password);

            if (newPassword != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.NewPassword), newPassword);

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData("EncryptAllWithPassword", null, null)]
        [InlineData("EncryptSensitiveWithPassword", null, null)]
        public void Fail_Validate_ProtectionLevelPasswordCombination(string protectionLevel, string password, string newPassword)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProtectionLevel), protectionLevel);

            if (password != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Password), password);

            if (newPassword != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.NewPassword), newPassword);

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, $"NewPassword or Password argument is required when argument ProtectionLevel is {protectionLevel}");
        }

        [Theory]
        [InlineData("DontSaveSensitive", "123", "123")]
        [InlineData("DontSaveSensitive", null, "123")]
        public void Fail_Validate_ProtectionLevelDontSaveSensitiveWithPassword(string protectionLevel, string password, string newPassword)
        {
            var filePath = Path.Combine(_workingFolder, "test.dtproj");
            File.WriteAllText(filePath, "test");

            var buildArguments = Helpers.CreateBuildArguments();
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProjectPath), "test.dtproj");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Configuration), "abc");

            // must have to pass validation
            Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.ProtectionLevel), protectionLevel);

            if (password != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.Password), password);

            if (newPassword != null)
                Helpers.SetBuildArgumentsValue(buildArguments, nameof(buildArguments.NewPassword), newPassword);

            var exception = Record.Exception(() => _validateMethod.Invoke(buildArguments, new object[] { }));

            Assert.NotNull(exception);

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Assert.IsType<InvalidArgumentException>(exception);
            Assert.Equal(exception.Message, "NewPassword argument should not be specified when ProtectionLevel is DontSaveSensitive.");
        }
        #endregion // Validate tests

        #region ProcessArgs Tests

        [Fact]
        public void Fail_ProcessArgs_InvalidProjectPath()
        {
            var exception = Record.Exception(() => BuildArguments.ProcessArgs(new [] { "abc.dtproj", "-configuration", "abc"}));

            Assert.NotNull(exception);
            Assert.IsType<FileNotFoundException>(exception);
        }

        [Fact]
        public void Fail_ProcessArgs_NullArgs()
        {
            var exception = Record.Exception(() => BuildArguments.ProcessArgs(null));

            Assert.NotNull(exception);
            Assert.IsType<NullReferenceException>(exception);

        }
        #endregion // ProcessArgs Tests
    }
}
