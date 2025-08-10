using Mugnum.TarkovMods.WttArmoryStripper;

var currentDir = Environment.CurrentDirectory;
var gamePath = CleanupManager.ContainsEftExecutable(currentDir)
	? currentDir
	: @"e:\Games\Escape From Tarkov SP 3.11.3";

var weaponsToRemove = new[]
{
	"WeaponAK5C.json"
	,"WeaponCarmel.json"
	,"WeaponCZScorpion.json"
	,"WeaponDragunov.json"
	,"WeaponG3.json"
	,"WeaponHK417.json"
	,"WeaponKACPDW.json"
	,"WeaponPatriot.json"
	,"WeaponPM9.json"
	,"WeaponRemingtonACR.json"
	,"WeaponWagesOfSin.json"
	,"WeaponX95.json"
	,"WeaponXM8.json"
	,"WTT_Posters.json"
};

new CleanupManager(gamePath, weaponsToRemove).Run();
Console.WriteLine();
Console.WriteLine("Finished processing. Press any key to exit...");
Console.ReadKey();
