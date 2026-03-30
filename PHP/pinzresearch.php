<?php 

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$sql = "select * from pinzresearch";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
while($row = mysql_fetch_assoc($res)) {
	echo $row["Symbol"] . "\037" . $row["Ago"] . "\037" . $row["Direction"] . "\037" . $row["Comment"] .  "\037" . $row["Description"] . "\n";
}

?>

