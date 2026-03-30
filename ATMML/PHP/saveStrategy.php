<?php 

$name = $_GET["n"];
$value = $_GET["v"];

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$sql  = "insert into strategies (Name, Value) values ('$name', '$value') ";
$sql .= "on duplicate key update Value = '$value'";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
else echo $sql;

?>
