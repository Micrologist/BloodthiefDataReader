using MemUtil;
using System.Diagnostics;
using System.Numerics;

Console.Clear();

var proc = Process.GetProcessesByName("bloodthief_v0.01.x86_64").FirstOrDefault();

if (proc is null)
{
	Console.WriteLine("Game process not found...");
	Console.ReadKey();
	return;
}

var scn = new SignatureScanner(proc, proc.MainModule.BaseAddress, proc.MainModule.ModuleMemorySize);
var sceneTreeTrg = new SigScanTarget(3, "48 8b 05 ?? ?? ?? ?? 48 8b b7 ?? ?? ?? ?? 48 89 fb 48 89 d5") { OnFound = (p, s, ptr) => ptr + 0x4 + proc.Read<int>(ptr) };
var sceneTreePtr = scn.Scan(sceneTreeTrg);

if (sceneTreePtr == IntPtr.Zero)
{
	Console.WriteLine("SceneTree not found...");
	Console.ReadKey();
	return;
}

Console.WriteLine($"SceneTree found at 0x{sceneTreePtr:X}");

//Follow the pointer
var SceneTree = proc.ReadValue<IntPtr>((IntPtr)sceneTreePtr);

//SceneTree.root
var rootWindow = proc.ReadValue<IntPtr>((IntPtr)SceneTree + 0x2D0);

//We are starting from the rootwindow node, its children are the scene root nodes
var childCount = proc.ReadValue<int>((IntPtr)rootWindow + 0x190);
var childArrayPtr = proc.ReadValue<IntPtr>((IntPtr)rootWindow + 0x198);

//Iterating through all scene root nodes to find the GameManager and EndLevelScreen nodes
//Caching here only works because the nodes aren't ever destroyed/created at runtime
var GameManagerObj = IntPtr.Zero;
var EndLevelScreen = IntPtr.Zero;

for (int i = 0; i < childCount; i++)
{
	var child = proc.ReadValue<IntPtr>(childArrayPtr + (0x8 * i));
	var childName = ReadStringName(proc.ReadValue<IntPtr>(child + 0x1F0));

	if (childName == "GameManager")
	{
		GameManagerObj = child;
	}
	else if (childName == "EndLevelScreen")
	{
		EndLevelScreen = child;
	}
}

if (GameManagerObj == IntPtr.Zero || EndLevelScreen == IntPtr.Zero)
{
	//This should only happen during game boot
	Console.WriteLine("GameManager/EndLevelScreen not found - trying again!");
	Console.ReadKey();
	return;
}

Console.WriteLine($"GameManager found at 0x{GameManagerObj:X}\n" +
	$"EndLevelScreen found at 0x{EndLevelScreen:X}");

//This grabs the GDScriptInstance attached to the GameManager Node
var GameManager = proc.ReadValue<IntPtr>((IntPtr)GameManagerObj + 0x68);

//Vector<Variant> GDScriptInstance.members
var gameManagerMemberArray = proc.ReadValue<IntPtr>((IntPtr)GameManager + 0x28);

Console.WriteLine();

while (proc != null && !proc.HasExited)
{

	var currentSceneNode = proc.ReadValue<IntPtr>(SceneTree + 0x3C0);
	var scene = ReadStringName(proc.ReadValue<IntPtr>(currentSceneNode + 0x1F0));

	var checkpointNum = proc.ReadValue<int>(gameManagerMemberArray + 0x230);

	var igt = (proc.ReadValue<double>(gameManagerMemberArray + 0xE0) - 7.2) / 13.3; ;

	var levelFinished = proc.ReadValue<bool>(EndLevelScreen + 0x41C);

	var playerPtr = proc.ReadValue<IntPtr>(gameManagerMemberArray + 0x28);
	var xVel = proc.ReadValue<float>(playerPtr + 0x5E8);
	var zVel = proc.ReadValue<float>(playerPtr + 0x5F0);
	var speed = Math.Sqrt((xVel * xVel) + (zVel * zVel));

	var consoleWidth = Console.WindowWidth;
	Console.SetCursorPosition(0, 4);
	Console.WriteLine($"scene: {scene}".PadRight(consoleWidth));
	Console.WriteLine($"checkpointNum: {checkpointNum}".PadRight(consoleWidth));
	Console.WriteLine($"igt: {igt:0.00}".PadRight(consoleWidth));
	Console.WriteLine($"levelFinished: {levelFinished}".PadRight(consoleWidth));
	Console.WriteLine($"speed: {speed:0.0} m/s".PadRight(consoleWidth));

	Thread.Sleep(50);
}

//String Names contain a pointer to a utf32 encoded string, this is ugly but works well enough
string ReadStringName(IntPtr ptr)
{
	var output = "";
	var charPtr = proc.ReadValue<IntPtr>((IntPtr)ptr + 0x10);

	while (proc.ReadValue<int>((IntPtr)charPtr) != 0)
	{
		output += proc.ReadValue<char>(charPtr);
		charPtr += 0x4;
	}

	return output;
}