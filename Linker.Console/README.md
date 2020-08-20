# Linker Console Playground

This application is configured for aggressive linking (it will trim unused everything). This includes removing:

- Assemblies
- Types
- Members from types

To publish the application, use:

**Windows:**

```
dotnet publish -r win-x64
```

**Linux:**

```
dotnet publish -r linux-x64
```

You can also publish the application as single file (well single-ish file):

```
dotnet publish /p:PublishSingleFile=true -r win-x64
```

This will dump a whole set of warnings into the console. You can pipe this to a file instead of it's too spammy. The warnings should
look like this:

```
ILLink : Trim analysis warning IL2006: System.RuntimeTypeHandle.GetTypeHelper(Type,Type[],IntPtr,Int32): Calling to 'System.Type.MakeGenericType' on unrecognized value. [C:\Users\david\source\repos\CommunityStandup\Linker.Console\Linker.Console.csproj]
ILLink : Trim analysis warning IL2006: System.RuntimeType.GetMethodBase(RuntimeType,RuntimeMethodHandleInternal): The parameter 'reflectedType' of method 'System.RuntimeType.GetMethodBase(RuntimeType,RuntimeMethodHandleInternal)' with dynamically accessed member kinds 'None' is passed into the implicit 'this' parameter of method 'System.Type.GetMember(String,MemberTypes,BindingFlags)' which requires dynamically accessed member kinds 'All'. To fix this add DynamicallyAccessedMembersAttribute to it and specify at least these member kinds 'All'. [C:\Users\david\source\repos\CommunityStandup\Linker.Console\Linker.Console.csproj]
ILLink : Trim analysis warning IL2006: System.RuntimeType.ForwardCallToInvokeMember(String,BindingFlags,Object,Object[],Boolean[],Int32[],Type[],Type): The implicit 'this' parameter of method 'System.RuntimeType.ForwardCallToInvokeMember(String,BindingFlags,Object,Object[],Boolean[],Int32[],Type[],Type)' with dynamically accessed member kinds 'None' is passed into the implicit 'this' parameter of method 'System.Type.InvokeMember(String,BindingFlags,Binder,Object,Object[],ParameterModifier[],CultureInfo,String[])' which requires dynamically accessed member kinds 'All'. To fix this add DynamicallyAccessedMembersAttribute to it and specify at least these member kinds 'All'. [C:\Users\david\source\repos\CommunityStandup\Linker.Console\Linker.Console.csproj]
```

To run the application run this executable:

```
bin\Debug\net5.0\win-x64\publish\Linker.Console.exe
```

or

```
bin\Debug\net5.0\linux-x64\publish\Linker.Console.exe
```

I highly recommand looking at the results of various dlls in the de-compiler like ILSpy or dnspy!