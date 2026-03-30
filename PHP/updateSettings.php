<?php 

$user = $_GET["u"];
$settings = $_GET["s"];


/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$rows = split("\036", $settings);

echo "Row Count = " . count($rows) . "\n";

$res = 1;
foreach ($rows as $row) {
  echo "Row = " . $row . "\n";
  $fields = split("\037", $row);
  
  echo "Field Count = " . count($fields) . "\n";
  
  if (count($fields) >= 2) {
    $key = $fields[0];
    $value = $fields[1]; 
    $sql = "insert into `settings`(`User`, `Key`, `Value`) values ('$user', '$key', '$value') on duplicate key update `Value` = '$value'";
    $res = mysql_query($sql, $link);
    if (!$res) break;
  }
}

if (!$res) echo mysql_error();
else echo "ok";

?>
