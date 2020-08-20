using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            var mytype = new MyType();
            mytype.MyMethod();

            var reflectionClass = new SimpleReflection();
            reflectionClass.Method();

            var advanced = new AdvancedReflection(typeof(ClassWithATwoMethod));
            advanced.Method();

            var reflectionOnFramework = new ReflectionOnTheFramework();
            reflectionOnFramework.Method();

            var generics = new ClassWithGenerics<MyClass>();
            var myclass = generics.Create();
            myclass.Method();

            var complexGenerics = new ComplexGenerics<MyOtherClass>();
            var otherClass = complexGenerics.Create();
            otherClass.Method();
        }
    }

    // This will be removed because it isn't used by anywhere
    public class Thing
    {

    }

    public class MyType
    {
        // This should be kept
        public void MyMethod()
        {
            Console.WriteLine($"{nameof(MyType)}.{nameof(MyMethod)}");
        }

        // This will be removed because it isn't used
        public void MyOtherMethod()
        {
            new MyOtherType().SomethingUseful();
        }
    }

    // This will be removed because it's not used as well (even though it's called from MyOtherMethod)
    public class MyOtherType
    {
        public void SomethingUseful()
        {
            Console.WriteLine($"{nameof(MyOtherType)}.{nameof(SomethingUseful)}");
        }
    }

    #region Generics

    // This will not work because we're using reflection to create T and haven't told the linker
    // to preserve T's constructor.
    // we need to change add this attribute:
    // ClassWithGenerics<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]T>
    // Once you apply this attribute, it will tell the linker to preserve the parameterless constructor of the specified type. It will also
    // force all other generic pieces of code that use ClassWithGenerics to be annotated.
    public class ComplexGenerics<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]T>
    {
        public T Create() => Activator.CreateInstance<T>();
    }

    // The compiler can detect the generic constraint new so it knows it has to keep T's default constructor.
    public class ClassWithGenerics<T> where T : new()
    {
        public T Create() => new T();
    }

    public class MyOtherClass
    {
        public void Method()
        {
            Console.WriteLine($"{nameof(MyOtherClass)}.{nameof(Method)}");
        }
    }

    public class MyClass
    {
        public void Method()
        {
            Console.WriteLine($"{nameof(MyClass)}.{nameof(Method)}");
        }
    }

    #endregion

    #region Reflection 

    public class SimpleReflection
    {
        public void Method()
        {
            Console.WriteLine($"{nameof(SimpleReflection)}.{nameof(Method)}");

            // The linker will recognize this simple reflection pattern and preserve the MethodTwo method
            typeof(SimpleReflection).GetMethod("MethodTwo").Invoke(this, Array.Empty<object>());
        }

        public void MethodTwo()
        {
            Console.WriteLine($"{nameof(SimpleReflection)}.{nameof(MethodTwo)}!");
        }
    }

    public class ClassWithATwoMethod
    {
        public void MethodTwo()
        {
            Console.WriteLine($"{nameof(ClassWithATwoMethod)}.{nameof(MethodTwo)}!");
        }
    }

    public class AdvancedReflection
    {
        private readonly MethodInfo _methodInfo;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicMethods)]
        private readonly Type _type;

        // This will break the linker, we're passing in a type argument and we haven't told it what to preserve. To fix this
        // we need to change add this attribute:
        // public AdvancedReflection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
        // Once you apply this attribute, it will tell the linker to preserve the public methods and parameterless constructor of the specified type. It will also
        // force all other pieces of code that create AdvancedReflection to be annotated.
        public AdvancedReflection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
        {
            _type = type;
            _methodInfo = type.GetMethod("MethodTwo");
        }

        public void Method()
        {
            Console.WriteLine($"{nameof(AdvancedReflection)}.{nameof(Method)}");
            var instance = Activator.CreateInstance(_type);

            _methodInfo.Invoke(instance, Array.Empty<object>());
        }
    }


    public class ReflectionOnTheFramework
    {
        // This method does reflection on the Framework itself that the linker can't detect. We're calling Regex.Escape dynamically
        // and we need to tell the linker about it using the new DynamicDependencyAttribute
        [DynamicDependency("Escape", typeof(Regex))]
        public void Method()
        {
            var ns = "System.Text.RegularExpressions";

            var type = Type.GetType($"{ns}.Regex, {ns}");

            // Find the Regex.Escape method
            var escapeMethod = type.GetMethod("Escape", BindingFlags.Static | BindingFlags.Public);

            var s = (string)escapeMethod.Invoke(null, new object[] { "\\w+" });

            Console.WriteLine(s);
        }
    }


    #endregion

}
